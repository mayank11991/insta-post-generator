import re

# Fix AboutPage.xaml
with open('Views/AboutPage.xaml', 'r') as f:
    content = f.read()

# Do targeted replacements for the specific patterns that appear in the file
# Replace " & " (space ampersand space) with " & "
content = content.replace(' & ', ' & ')
content = content.replace(' &', ' & ')  # already replaced

# Replace & at end of text before quote
content = content.replace('&"', '&"')

# Replace & at end of line
content = content.replace('&\n', '&\n')

# Also handle " & " if it somehow got in there
content = content.replace(' & ', ' & ')

with open('Views/AboutPage.xaml', 'w') as f:
    f.write(content)

print('Fixed AboutPage.xaml')

# Fix MainPage.xaml
with open('Views/MainPage.xaml', 'r') as f:
    content = f.read()

content = content.replace(' & ', ' & ')
content = content.replace('&"', '&"')
content = content.replace('&\n', '&\n')
content = content.replace(' & ', ' & ')

with open('Views/MainPage.xaml', 'w') as f:
    f.write(content)

print('Fixed MainPage.xaml')