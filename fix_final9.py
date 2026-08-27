# Fix AboutPage.xaml
with open('Views/AboutPage.xaml', 'r') as f:
    content = f.read()

# Replace literal ampersand character (U+0026) with & entity
content = content.replace('\u0026 ', '\u0026 ')
content = content.replace('\u0026"', '\u0026"')
content = content.replace('\u0026\n', '\u0026\n')

with open('Views/AboutPage.xaml', 'w') as f:
    f.write(content)

print('Fixed AboutPage.xaml')

# Fix MainPage.xaml
with open('Views/MainPage.xaml', 'r') as f:
    content = f.read()

content = content.replace('\u0026 ', '\u0026 ')
content = content.replace('\u0026"', '\u0026"')
content = content.replace('\u0026\n', '\u0026\n')

with open('Views/MainPage.xaml', 'w') as f:
    f.write(content)

print('Fixed MainPage.xaml')