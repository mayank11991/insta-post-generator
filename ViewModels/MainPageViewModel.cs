using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using InstaPostGenerator.Models;
using InstaPostGenerator.Services;
#if ANDROID
using Android.Provider;
#endif

namespace InstaPostGenerator.ViewModels;

public class MainPageViewModel : INotifyPropertyChanged
{
    private bool _isGenerating;
    private string _statusMessage = "Ready to generate posts";
    private ObservableCollection<PostItemViewModel> _posts = new();
    private int _liveCount;

    public MainPageViewModel()
    {
        // Initialize categories from remote config
        var config = RemoteConfigService.GetConfig();
        Categories = new ObservableCollection<CategorySelection>();
        foreach (var kv in config.Categories)
        {
            Categories.Add(new CategorySelection
            {
                Name = kv.Key,
                DisplayName = $"{kv.Value.Emoji} {kv.Value.DisplayName}",
                IsSelected = Categories.Count == 0
            });
        }

        GenerateCommand = new Command(async () => await GeneratePostsAsync(), () => !IsGenerating);
        TestImageCommand = new Command(async () => await GenerateTestImageAsync(), () => !IsGenerating);
        ToggleCategoryCommand = new Command<CategorySelection>(c => 
        {
            if (c == null) return;
            foreach (var cat in Categories)
                cat.IsSelected = false;
            c.IsSelected = true;
        });

        // Load saved posts
        LoadSavedPosts();
    }

    public ObservableCollection<CategorySelection> Categories { get; }
    public ObservableCollection<PostItemViewModel> Posts
    {
        get => _posts;
        set { _posts = value; OnPropertyChanged(); }
    }

    public bool IsGenerating
    {
        get => _isGenerating;
        set { _isGenerating = value; OnPropertyChanged(); ((Command)GenerateCommand).ChangeCanExecute(); }
    }

    private double _generateProgress;
    public double GenerateProgress
    {
        get => _generateProgress;
        set { _generateProgress = value; OnPropertyChanged(); _liveCount = (int)(value * 10); OnPropertyChanged(nameof(GenerateButtonText)); OnPropertyChanged(nameof(ShowTick)); }
    }

    public string GenerateButtonText => IsDone ? "✓ Done" : IsGenerating ? $"Generating... {_liveCount}/10" : "Generate Posts";
    public bool ShowTick => IsDone;

    private bool _isDone;
    public bool IsDone
    {
        get => _isDone;
        set { _isDone = value; OnPropertyChanged(); OnPropertyChanged(nameof(GenerateButtonText)); OnPropertyChanged(nameof(ShowTick)); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public ICommand GenerateCommand { get; }
    public ICommand TestImageCommand { get; }
    public ICommand ToggleCategoryCommand { get; }

    private async Task GenerateTestImageAsync()
    {
        if (IsGenerating) return;
        IsGenerating = true;
        try
        {
            var outputDir = Config.GetOutputDir();
            Directory.CreateDirectory(outputDir);
            var testPath = Path.Combine(outputDir, "test_image.png");
            PostGenerator.GenerateTestImage(testPath);
            StatusMessage = $"Test image saved: {testPath}";
            await ShowToastAsync($"Test image saved to:\n{testPath}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            await ShowToastAsync($"Error: {ex.Message}");
        }
        finally
        {
            IsGenerating = false;
        }
    }

    public async Task GeneratePostsAsync()
    {
        if (IsGenerating) return;

        IsGenerating = true;
        IsDone = false;
        GenerateProgress = 0;
        _liveCount = 0;
        StatusMessage = "Starting post generation...";
        Posts.Clear();

        try
        {
            var selectedCategories = Categories.Where(c => c.IsSelected).Select(c => c.Name).ToList();
            Log($"Selected categories: {string.Join(", ", selectedCategories)}");
            if (!selectedCategories.Any())
            {
                await ShowToastAsync("Please select at least one category");
                return;
            }

            var outputDir = Config.GetOutputDir();
            Log($"Output dir: {outputDir}");
            StatusMessage = $"Output: {outputDir}";
            if (Directory.Exists(outputDir))
            {
                try { Directory.Delete(outputDir, true); } catch { }
            }
            Directory.CreateDirectory(outputDir);

            var seen = LoadSeenStore();
            Log($"Seen store: {seen.Ids.Count} ids, {seen.Titles.Count} titles");
            var mix = new ContentMixTracker();

            var allPosts = new List<PostDisplayItem>();
            int globalIndex = 0;

            foreach (var category in selectedCategories)
            {
                StatusMessage = $"Fetching news for {category}...";
                Log($"=== Starting category: {category} ===");
                
                var fetchMore = new Func<Task<List<Article>>>(async () =>
                {
                    return new List<Article>();
                });

                List<Article> initialResults;
                try
                {
                    StatusMessage = $"Calling API for {category}...";
                    initialResults = await NewsFetcher.FetchResultsAsync(category, maxPages: 2, fetchMore);
                    Log($"Fetched {initialResults.Count} articles for {category}");
                }
                catch (Exception ex)
                {
                    Log($"FETCH ERROR: {ex}");
                    StatusMessage = $"Fetch error: {ex.Message}";
                    await ShowToastAsync($"API Error: {ex.Message}");
                    break;
                }
                
                StatusMessage = $"Fetched {initialResults.Count} articles. Filtering...";

                List<Article> articles;
                try
                {
                    articles = await NewsFetcher.PickFreshArticlesAsync(initialResults, seen, limit: Config.POSTS_PER_RUN, fetchMore);
                    Log($"After PickFresh: {articles.Count} articles");
                }
                catch (Exception ex)
                {
                    Log($"FILTER ERROR: {ex}");
                    StatusMessage = $"Filter error: {ex.Message}";
                    await Task.Delay(3000);
                    continue;
                }

                StatusMessage = $"Found {articles.Count} fresh articles";
                Log($"Processing {articles.Count} articles...");

                if (!articles.Any())
                {
                    Log($"No articles passed filters for {category}. Checking why...");
                    foreach (var a in initialResults.Take(5))
                    {
                        Log($"  Sample: title='{a.Title?.Substring(0, Math.Min(40, a.Title?.Length ?? 0))}' thumb='{(string.IsNullOrEmpty(a.Thumbnail) ? "EMPTY" : "OK")}' link='{(string.IsNullOrEmpty(a.Link) ? "EMPTY" : "OK")}'");
                    }
                    continue;
                }

                foreach (var article in articles)
                {
                    try
                    {
                        Log($"Processing: {article.Title?.Substring(0, Math.Min(40, article.Title?.Length ?? 0))}");
                        StatusMessage = $"[{allPosts.Count + 1}/10] Processing: {(article.Title?.Length > 50 ? article.Title[..50] : article.Title)}...";

                        var processed = ContentEngine.ProcessArticle(article, mix);
                        Log($"  Category={processed.Category} Hook={processed.Hook?.Substring(0, Math.Min(30, processed.Hook?.Length ?? 0))} Priority={processed.Priority} Quality={processed.QualityPassed}");
                        
                        if (!processed.QualityPassed)
                        {
                            var hardIssues = processed.QualityIssues.Where(i => !i.Contains("proceeding anyway")).ToList();
                            if (hardIssues.Any())
                            {
                                Log($"  SKIPPED quality: {string.Join("; ", hardIssues)}");
                                continue;
                            }
                        }

                        if (processed.Priority < Config.MIN_ARTICLE_SCORE)
                        {
                            Log($"  SKIPPED priority {processed.Priority} < {Config.MIN_ARTICLE_SCORE}");
                            continue;
                        }

                        var slug = SafeSlug(article.Title);
                        var imagePath = Path.Combine(outputDir, $"{globalIndex + 1:D2}_{slug}.png");
                        
                        StatusMessage = $"[{allPosts.Count + 1}/10] Generating image...";
                        Log($"  Generating image: {imagePath}");
                        await PostGenerator.CreateNewsImageAsync(processed, imagePath, template: 0, processed.TemplateIds);
                        Log($"  Image saved: {File.Exists(imagePath)}");

                        var summaryText = await NewsFetcher.SummarizeForCaptionAsync(article.Link);
                        var caption = ContentEngine.BuildSmartCaption(
                            article, processed.Category, processed.Hook, processed.CTA, processed.Hashtags, summaryText);

                        var articleId = NewsFetcher.ExtractArticleId(article);
                        seen.Ids.Add(articleId);
                        seen.Titles.Add(article.Title.ToLowerInvariant().Trim());

                        var displayItem = new PostDisplayItem
                        {
                            Index = globalIndex + 1,
                            ImagePath = imagePath,
                            Caption = caption,
                            SourceUrl = article.Link,
                            Hashtags = string.Join(" ", processed.Hashtags),
                            CategoryLabel = processed.CategoryLabel,
                            Hook = processed.Hook,
                            SeriesName = processed.SeriesName
                        };

                        allPosts.Add(displayItem);
                        globalIndex++;
                        GenerateProgress = (double)allPosts.Count / 10;
                        Log($"  POST {allPosts.Count}/10 CREATED: {slug}");

                        StatusMessage = $"[{allPosts.Count}/10] Generated: {slug}";

                        if (allPosts.Count >= 10) break;
                    }
                    catch (Exception ex)
                    {
                        Log($"  ERROR processing article: {ex}");
                        StatusMessage = $"Error: {ex.Message}";
                        await Task.Delay(1000);
                    }
                }

                if (allPosts.Count >= 10) break;
            }

            SaveSeenStore(seen);
            mix.Save();

            // Scan media so images appear in gallery
            ScanMediaGallery(allPosts);

            Posts.Clear();
            foreach (var post in allPosts)
            {
                Posts.Add(new PostItemViewModel(post, Posts.Count, this));
            }

            // Save posts for persistence
            SavePosts(allPosts);

            Log($"DONE: {Posts.Count} posts generated");
            StatusMessage = allPosts.Count > 0
                ? $"Generated {Posts.Count} posts successfully!"
                : $"0 posts generated. Check debug.log for details.";
            IsDone = allPosts.Count > 0;
        }
        catch (Exception ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            Log($"FATAL: {ex}");
            StatusMessage = $"Error: {message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private SeenStore LoadSeenStore()
    {
        var path = Config.GetSeenFile();
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                return Newtonsoft.Json.JsonConvert.DeserializeObject<SeenStore>(json) ?? new SeenStore();
            }
            catch { }
        }
        return new SeenStore();
    }

    private void SaveSeenStore(SeenStore seen)
    {
        var path = Config.GetSeenFile();
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(seen, Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText(path, json);
    }

    private string GetPostsFile()
    {
        return Path.Combine(Config.GetOutputDir(), "saved_posts.json");
    }

    private void SavePosts(List<PostDisplayItem> posts)
    {
        try
        {
            var path = GetPostsFile();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(posts, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(path, json);
        }
        catch { }
    }

    private void LoadSavedPosts()
    {
        try
        {
            var path = GetPostsFile();
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var items = Newtonsoft.Json.JsonConvert.DeserializeObject<List<PostDisplayItem>>(json);
                if (items != null && items.Any())
                {
                    Posts.Clear();
                    foreach (var item in items)
                    {
                        if (File.Exists(item.ImagePath))
                            Posts.Add(new PostItemViewModel(item, Posts.Count, this));
                    }
                    StatusMessage = $"Loaded {Posts.Count} saved posts";
                }
            }
        }
        catch { }
    }

    private static string SafeSlug(string text, int maxLen = 40)
    {
        if (string.IsNullOrEmpty(text)) return "post";
        var slug = Regex.Replace(text, @"[^a-zA-Z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-").Trim().ToLowerInvariant();
        return slug.Length > maxLen ? slug.Substring(0, maxLen).TrimEnd('-') : slug;
    }

    private static void Log(string msg)
    {
        try
        {
            var logDir = Config.GetOutputDir();
            if (!Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);
            File.AppendAllText(Path.Combine(logDir, "debug.log"), $"{DateTime.Now:HH:mm:ss} {msg}\n");
        }
        catch
        {
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "instapost_debug.log"), $"{DateTime.Now:HH:mm:ss} {msg}\n"); } catch { }
        }
    }

    public Task ShowToastAsync(string message)
    {
        return Shell.Current.DisplayAlert("InstaPost Generator", message, "OK");
    }

    private void ScanMediaGallery(List<PostDisplayItem> posts)
    {
#if ANDROID
        try
        {
            foreach (var post in posts)
            {
                if (File.Exists(post.ImagePath))
                {
                    var values = new Android.Content.ContentValues();
                    values.Put(MediaStore.Images.Media.InterfaceConsts.Data, post.ImagePath);
                    values.Put(MediaStore.Images.Media.InterfaceConsts.MimeType, "image/png");
                    values.Put(MediaStore.Images.Media.InterfaceConsts.DateAdded, Java.Lang.JavaSystem.CurrentTimeMillis() / 1000);

                    var resolver = Android.App.Application.Context.ContentResolver;
                    resolver.Insert(MediaStore.Images.Media.ExternalContentUri, values);
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Media scan error: {ex.Message}");
        }
#endif
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class CategorySelection : INotifyPropertyChanged
{
    private bool _isSelected;

    public string Name { get; set; }
    public string DisplayName { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}