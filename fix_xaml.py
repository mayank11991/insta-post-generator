import re

# Fix AboutPage.xaml
with open('Views/AboutPage.xaml', 'r') as f:
    content = f.read()

# Replace literal & with & but leave existing entities alone
# Match & that is NOT followed by # (numeric entity) or valid named entity
content = re.sub(r'&(?!(?:#x?\w+|amp|lt|gt|quot|apos);)', '&', content)

with open('Views/AboutPage.xaml', 'w') as f:
    f.write(content)

print('Fixed AboutPage.xaml')

# Fix MainPage.xaml
with open('Views/MainPage.xaml', 'r') as f:
    content = f.read()

content = re.sub(r'&(?!(?:#x?\w+|amp|lt|gt|quot|apos);)', '&', content)

with open('Views/MainPage.xaml', 'w') as f:
    f.write(content)

print('Fixed MainPage.xaml')