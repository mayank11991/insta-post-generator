# Fix AboutPage.xaml
with open('Views/AboutPage.xaml', 'r') as f:
    content = f.read()

# Replace literal ampersand character (U+0026) with & entity
# Use actual unicode character for replacement
content = content.replace('& ', '& ')
content = content.replace('&"', '&"')
content = content.replace('&\n', '&\n')

with open('Views/AboutPage.xaml', 'w') as f:
    f.write(content)

print('Fixed AboutPage.xaml')

# Fix MainPage.xaml
with open('Views/MainPage.xaml', 'r') as f:
    content = f.read()

content = content.replace('& ', '& ')
content = content.replace('&"', '&"')
content = content.replace('&\n', '&\n')

with open('Views/MainPage.xaml', 'w') as f:
    f.write(content)

print('Fixed MainPage.xaml')