import re

# Fix AboutPage.xaml
with open('Views/AboutPage.xaml', 'r') as f:
    content = f.read()

# Replace all literal & that are not part of valid XML entities
# Valid entities: &#xA; & < > " &apos;
# Pattern: & NOT followed by valid entity pattern ending with ;
content = re.sub(r'&(?!(?:#x?\d+|amp|lt|gt|quot|apos);)', '&', content)

with open('Views/AboutPage.xaml', 'w') as f:
    f.write(content)

print('Fixed AboutPage.xaml')

# Fix MainPage.xaml
with open('Views/MainPage.xaml', 'r') as f:
    content = f.read()

content = re.sub(r'&(?!(?:#x?\d+|amp|lt|gt|quot|apos);)', '&', content)

with open('Views/MainPage.xaml', 'w') as f:
    f.write(content)

print('Fixed MainPage.xaml')