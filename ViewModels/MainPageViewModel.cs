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
    private bool _isPostMode = true;

    public MainPageViewModel()
    {
        // Initialize categories from remote config (default to post mode)
        LoadCategories("post");

        GenerateCommand = new Command(async () => await GeneratePostsAsync(), () => !IsGenerating);
        TestImageCommand = new Command(async () => await GenerateTestImageAsync(), () => !IsGenerating);
        ToggleCategoryCommand = new Command<CategorySelection>(c => 
        {
            if (c == null) return;
            foreach (var cat in Categories)
                cat.IsSelected = false;
            c.IsSelected = true;
        });
        PostCarouselCommand = new Command(async () => await PostCarouselAsync(), () => CanPostCarousel && !IsGenerating);
        TogglePostSelectionCommand = new Command<PostItemViewModel>(p => 
        {
            if (p != null)
            {
                p.IsSelected = !p.IsSelected;
            }
        });
        SetPostModeCommand = new Command(() => SetMode("post"));
        SetReelModeCommand = new Command(() => SetMode("reel"));

        // Load saved posts
        LoadSavedPosts();
    }

    private void LoadCategories(string contentType)
    {
        Categories.Clear();
        var config = RemoteConfigService.GetConfig();
        foreach (var kv in config.Categories)
        {
            if (kv.Value.ContentType == contentType)
            {
                Categories.Add(new CategorySelection
                {
                    Name = kv.Key,
                    DisplayName = $"{kv.Value.Emoji} {kv.Value.DisplayName}",
                    IsSelected = Categories.Count == 0
                });
            }
        }
    }

    private void SetMode(string mode)
    {
        IsPostMode = mode == "post";
        LoadCategories(mode);
        OnPropertyChanged(nameof(IsPostMode));
        OnPropertyChanged(nameof(ModeLabel));
        OnPropertyChanged(nameof(GenerateButtonText));
    }

    public string ModeLabel => IsPostMode ? "📝 Posts" : "🎬 Reels";

    public int SelectedPostCount => Posts.Count(p => p.IsSelected);
    public bool CanPostCarousel => SelectedPostCount >= 2;

    public void OnPostSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedPostCount));
        OnPropertyChanged(nameof(CanPostCarousel));
        ((Command)PostCarouselCommand).ChangeCanExecute();
    }

    public ObservableCollection<CategorySelection> Categories { get; } = new();
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

    public string GenerateButtonText => IsDone ? "✓ Done" : IsGenerating ? $"Generating... {_liveCount}/10" : IsPostMode ? "Generate Posts" : "Generate Reels";
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
    public ICommand PostCarouselCommand { get; }
    public ICommand TogglePostSelectionCommand { get; }
    public ICommand SetPostModeCommand { get; }
    public ICommand SetReelModeCommand { get; }

    public bool IsPostMode
    {
        get => _isPostMode;
        set { _isPostMode = value; OnPropertyChanged(); }
    }

    private async Task GenerateTestImageAsync()
    {
        if (IsGenerating) return;
        IsGenerating = true;
        try
        {
            var outputDir = Config.GetOutputDir();
            Directory.CreateDirectory(outputDir);
            var testPath = Path.Combine(outputDir, "test_image.png");
            await PostGenerator.GenerateTestImageAsync(testPath);
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
            var posted = LoadPostedStore();
            Log($"Seen store: {seen.Ids.Count} ids, {seen.Titles.Count} titles");
            Log($"Posted store: {posted.Ids.Count} ids, {posted.Titles.Count} titles");
            var mix = new ContentMixTracker();

            var allPosts = new List<PostDisplayItem>();
            int globalIndex = 0;

            foreach (var category in selectedCategories)
            {
                var config = RemoteConfigService.GetConfig();
                var catConfig = config.Categories.GetValueOrDefault(category);
                var isImageOnly = catConfig?.Mode == "image_only";
                var isReelCategory = catConfig?.ContentType == "reel";

                StatusMessage = isReelCategory ? $"Fetching videos for {category}..." : isImageOnly ? $"Fetching HD images for {category}..." : $"Fetching news for {category}...";
                Log($"=== Starting category: {category} (imageOnly={isImageOnly}, isReel={isReelCategory}) ===");
                
                if (isReelCategory)
                {
                    // Fetch YouTube videos for reel categories (multiple queries for diversity)
                    var youtubeQueries = Config.GetCategoryYouTubeQueries(category);
                    if (youtubeQueries.Length == 0)
                        youtubeQueries = new[] { catConfig?.Query ?? category };

                    List<VideoItem> videos;
                    try
                    {
                        StatusMessage = $"Searching YouTube for {category}...";
                        var allVideos = new List<VideoItem>();
                        var seenIds = new HashSet<string>();

                        // Fetch from each query and deduplicate
                        foreach (var q in youtubeQueries)
                        {
                            Log($"  Query: {q}");
                            var fetched = await VideoFetcher.FetchYouTubeVideosAsync(q, 10);
                            foreach (var v in fetched)
                            {
                                if (seenIds.Add(v.VideoId))
                                    allVideos.Add(v);
                            }
                            if (allVideos.Count >= 15) break;
                        }

                        // Shuffle for diversity, take up to POSTS_PER_RUN
                        var rng = new Random();
                        videos = allVideos.OrderBy(_ => rng.Next()).Take(Config.POSTS_PER_RUN).ToList();

                        Log($"Fetched {videos.Count} unique diverse videos for {category}");
                    }
                    catch (Exception ex)
                    {
                        Log($"VIDEO FETCH ERROR: {ex}");
                        StatusMessage = $"Video fetch error: {ex.Message}";
                        break;
                    }

                    if (!videos.Any())
                    {
                        Log($"No videos found for {category}");
                        continue;
                    }

                    foreach (var video in videos)
                    {
                        try
                        {
                            if (allPosts.Count >= 10) break;

                            Log($"Processing video: {video.Title?.Substring(0, Math.Min(40, video.Title?.Length ?? 0))}");
                            StatusMessage = $"[{allPosts.Count + 1}/10] Processing: {(video.Title?.Length > 50 ? video.Title[..50] : video.Title)}...";

                            string videoPath = null;
                            string imagePath = null;

                            // Try to download video
                            try
                            {
                                videoPath = await VideoDownloader.DownloadVideoAsync(video.VideoUrl, outputDir);
                            }
                            catch { }

                            if (!string.IsNullOrEmpty(videoPath) && File.Exists(videoPath))
                            {
                                // Video downloaded successfully - process it
                                var trimmedPath = await VideoProcessor.TrimVideoAsync(videoPath, 90);
                                if (trimmedPath != videoPath && File.Exists(trimmedPath))
                                    videoPath = trimmedPath;

                                var reelPath = await VideoProcessor.ConvertToReelFormatAsync(videoPath);
                                if (reelPath != videoPath && File.Exists(reelPath))
                                    videoPath = reelPath;

                                imagePath = await VideoProcessor.ExtractThumbnailAsync(videoPath);
                                if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                                    imagePath = videoPath;
                            }
                            else
                            {
                                // Video download failed - generate image post instead
                                Log($"  Video download failed, generating image post");
                                StatusMessage = $"[{allPosts.Count + 1}/10] Generating image for: {(video.Title?.Length > 40 ? video.Title[..40] : video.Title)}...";

                                imagePath = Path.Combine(outputDir, $"{globalIndex + 1:D2}_{SafeSlug(video.Title)}.png");

                                // Create a ProcessedArticle-like object for image generation
                                var fakeArticle = new Article
                                {
                                    Title = video.Title,
                                    Summary = video.Description,
                                    Link = video.VideoUrl,
                                    Thumbnail = video.ThumbnailUrl,
                                    Source = new Models.SourceInfo { Name = video.ChannelName }
                                };
                                var processed = ContentEngine.ProcessArticle(fakeArticle, mix);
                                await PostGenerator.CreateNewsImageAsync(processed, imagePath, template: 0, processed.TemplateIds);
                            }

                            // Build caption for reel
                            var caption = $"🎬 {video.Title}\n\n📺 Channel: {video.ChannelName}\n\n{video.Description}";
                            var hashtags = $"#Reels #{category} #Viral #Trending #360buzz";

                            var displayItem = new PostDisplayItem
                            {
                                Index = globalIndex + 1,
                                ImagePath = imagePath ?? "",
                                VideoPath = videoPath ?? "",
                                Caption = caption,
                                SourceUrl = video.VideoUrl,
                                SourceName = video.ChannelName,
                                Hashtags = hashtags,
                                CategoryLabel = catConfig?.DisplayName ?? category,
                                Hook = video.Title,
                                SeriesName = $"{catConfig?.DisplayName ?? category} Reels",
                                IsReel = !string.IsNullOrEmpty(videoPath)
                            };

                            allPosts.Add(displayItem);
                            globalIndex++;
                            GenerateProgress = (double)allPosts.Count / 10;
                            Log($"  REEL {allPosts.Count}/10 CREATED: {SafeSlug(video.Title)}");
                            StatusMessage = $"[{allPosts.Count}/10] Generated: {video.Title?.Substring(0, Math.Min(30, video.Title?.Length ?? 0))}...";
                        }
                        catch (Exception ex)
                        {
                            Log($"  ERROR processing video: {ex}");
                            StatusMessage = $"Error: {ex.Message}";
                            await Task.Delay(1000);
                        }
                    }

                    if (allPosts.Count >= 10) break;
                }
                else
                {
                
                var fetchMore = new Func<Task<List<Article>>>(async () =>
                {
                    return new List<Article>();
                });

                List<Article> initialResults;
                try
                {
                    StatusMessage = isImageOnly ? $"Fetching celebrity images..." : $"Calling API for {category}...";
                    
                    if (isImageOnly)
                    {
                        initialResults = await NewsFetcher.FetchCelebrityImagesAsync(category, Config.POSTS_PER_RUN);
                    }
                    else
                    {
                        initialResults = await NewsFetcher.FetchResultsAsync(category, maxPages: 2, fetchMore);
                    }
                    Log($"Fetched {initialResults.Count} items for {category}");
                }
                catch (Exception ex)
                {
                    Log($"FETCH ERROR: {ex}");
                    StatusMessage = $"Fetch error: {ex.Message}";
                    await ShowToastAsync($"API Error: {ex.Message}");
                    break;
                }
                
                StatusMessage = $"Fetched {initialResults.Count} items. Filtering...";

                List<Article> articles;
                try
                {
                    if (isImageOnly)
                    {
                        articles = initialResults.Take(Config.POSTS_PER_RUN).ToList();
                    }
                    else
                    {
                        articles = await NewsFetcher.PickFreshArticlesAsync(initialResults, seen, posted, limit: Config.POSTS_PER_RUN, fetchMore);
                    }
                    Log($"After filtering: {articles.Count} articles");
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

                        var summaryText = "";
                        if (!isImageOnly)
                        {
                            summaryText = await NewsFetcher.SummarizeForCaptionAsync(article.Link);
                        }
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
                            SourceName = article.Source?.Name ?? "",
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

    private PostedStore LoadPostedStore()
    {
        var path = Config.GetPostedFile();
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                return Newtonsoft.Json.JsonConvert.DeserializeObject<PostedStore>(json) ?? new PostedStore();
            }
            catch { }
        }
        return new PostedStore();
    }

    private void SavePostedStore(PostedStore posted)
    {
        var path = Config.GetPostedFile();
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(posted, Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText(path, json);
    }

    public void MarkAsPosted(PostDisplayItem item)
    {
        try
        {
            var posted = LoadPostedStore();
            var articleId = NewsFetcher.ExtractArticleId(new Article { Title = item.Hook, Link = item.SourceUrl });
            if (!string.IsNullOrEmpty(articleId))
            {
                posted.Ids.Add(articleId);
                posted.Titles.Add(item.Hook.ToLowerInvariant().Trim());
                SavePostedStore(posted);
                Log($"Marked as posted: {articleId}");
            }
        }
        catch (Exception ex)
        {
            Log($"Error marking as posted: {ex}");
        }
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

    private async Task PostCarouselAsync()
    {
        if (IsGenerating) return;

        var selectedPosts = Posts.Where(p => p.IsSelected).ToList();
        if (selectedPosts.Count < 2)
        {
            await ShowToastAsync("Select at least 2 posts for carousel");
            return;
        }

        IsGenerating = true;
        StatusMessage = "Posting carousel...";

        try
        {
            var imagePaths = selectedPosts.Select(p => p.Item.ImagePath).ToArray();
            var caption = selectedPosts[0].Item.Caption;
            var hashtags = selectedPosts[0].Item.Hashtags;

            var result = await InstagramService.PostToInstagramAsync(
                imagePaths,
                caption,
                hashtags,
                status => StatusMessage = status);

            if (result.StartsWith("Posted!"))
            {
                foreach (var post in selectedPosts)
                {
                    post.IsPosted = true;
                    post.PostButtonText = "✅ Posted!";
                    MarkAsPosted(post.Item);
                }
                StatusMessage = $"Carousel posted! {selectedPosts.Count} images";
                await ShowToastAsync($"Carousel posted successfully!\n{selectedPosts.Count} images in one post");
            }
            else
            {
                StatusMessage = "Carousel post failed";
                await ShowToastAsync($"Failed: {result}");
            }
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