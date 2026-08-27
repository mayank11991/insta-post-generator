import re

# Fix AboutPage.xaml
with open('Views/AboutPage.xaml', 'r') as f:
    content = f.read()

# Replace literal & that are NOT part of valid XML entities
# Valid entity patterns: &#xNNNN; &#NNNN; & < > " &apos;
# Pattern: & NOT followed by valid entity start
# We want to replace & when followed by space, end, quote, or invalid entity start
content = re.sub(r'&(?!\s|&)(?!#x[\da-fA-F]+;)(?!#\d+;)(?!amp;)(?!lt;)(?!gt;)(?!quot;)(?!apos;)', '&', content)

with open('Views/AboutPage.xaml', 'w') as f:
    f.write(content)

print('Fixed AboutPage.xaml')

# Fix MainPage.xaml
with open('Views/MainPage.xaml', 'r') as f:
    content = f.read()

content = re.sub(r'&(?!\s|&)(?!#x[\da-fA-F]+;)(?!#\d+;)(?!amp;)(?!lt;)(?!gt;)(?!quot;)(?!apos;)', '&', content)

with open('Views/MainPage.xaml', 'w') as f:
    f.write(content)

print('Fixed MainPage.xaml')