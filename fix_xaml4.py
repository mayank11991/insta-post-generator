# Fix AboutPage.xaml
with open('Views/AboutPage.xaml', 'r') as f:
    content = f.read()

# Replace literal & (U+0026) with & entity
# Use actual ampersand character
content = content.replace('& ', '& ')
content = content.replace('&\n', '&\n')
content = content.replace('&\r', '&\r')
import re
content = re.sub(r'&(?=")', '&', content)

with open('Views/AboutPage.xaml', 'w') as f:
    f.write(content)

print('Fixed AboutPage.xaml')

# Fix MainPage.xaml
with open('Views/MainPage.xaml', 'r') as f:
    content = f.read()

content = content.replace('& ', '& ')
content = content.replace('&\n', '&\n')
content = content.replace('&\r', '&\r')
content = re.sub(r'&(?=")', '&', content)

with open('Views/MainPage.xaml', 'w') as f:
    f.write(content)

print('Fixed MainPage.xaml')