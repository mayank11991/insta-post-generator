import re

# Fix AboutPage.xaml
with open('Views/AboutPage.xaml', 'r') as f:
    content = f.read()

# Replace & that is NOT followed by a valid entity
# Valid entity patterns: &#xNNNN; &#NNNN; & < > " &apos;
# So & followed by: #x + hex + ; OR # + decimal + ; OR amp; OR lt; OR gt; OR quot; OR apos;
# We want to replace & that is NOT followed by these
# Match & that is followed by: whitespace, end, ", or letter that doesn't form valid entity
content = re.sub(r'&(?!\s)(?!(?:#x[\da-fA-F]+|#\d+|amp|lt|gt|quot|apos);)', '&', content)

with open('Views/AboutPage.xaml', 'w') as f:
    f.write(content)

print('Fixed AboutPage.xaml')

# Fix MainPage.xaml
with open('Views/MainPage.xaml', 'r') as f:
    content = f.read()

content = re.sub(r'&(?!\s)(?!(?:#x[\da-fA-F]+|#\d+|amp|lt|gt|quot|apos);)', '&', content)

with open('Views/MainPage.xaml', 'w') as f:
    f.write(content)

print('Fixed MainPage.xaml')