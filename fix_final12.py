# Fix AboutPage.xaml - target specific problematic literal ampersands
with open('Views/AboutPage.xaml', 'r') as f:
    content = f.read()

# The issue is literal & that are NOT followed by valid entity patterns
# Valid entity patterns: &#xNNNN; &#NNNN; & < > " &apos;
# We need to replace & that are followed by: space, end, ", or invalid start

import re

# Pattern: & that is NOT part of a valid entity
# Valid entities start with: #x + hex + ; OR # + decimal + ; OR amp; OR lt; OR gt; OR quot; OR apos;
# We want to replace & that is NOT followed by these

# First, protect valid entities by temporarily replacing them
# Replace valid entities with placeholders
protected = content
protected = re.sub(r'&#x[\da-fA-F]+;', '\u0000HEX\u0000', protected)
protected = re.sub(r'&#\d+;', '\u0000DEC\u0000', protected)
protected = re.sub(r'&', '\u0000AMP\u0000', protected)
protected = re.sub(r'<', '\u0000LT\u0000', protected)
protected = re.sub(r'>', '\u0000GT\u0000', protected)
protected = re.sub(r'"', '\u0000QUOT\u0000', protected)
protected = re.sub(r'&apos;', '\u0000APOS\u0000', protected)

# Now replace any remaining & with &
protected = protected.replace('&', '&')

# Restore protected entities
protected = protected.replace('\u0000HEX\u0000', '&#x')
protected = protected.replace('\u0000DEC\u0000', '&#')
protected = protected.replace('\u0000AMP\u0000', '&')
protected = protected.replace('\u0000LT\u0000', '<')
protected = protected.replace('\u0000GT\u0000', '>')
protected = protected.replace('\u0000QUOT\u0000', '"')
protected = protected.replace('\u0000APOS\u0000', '&apos;')

with open('Views/AboutPage.xaml', 'w') as f:
    f.write(protected)

print('Fixed AboutPage.xaml')

# Fix MainPage.xaml
with open('Views/MainPage.xaml', 'r') as f:
    content = f.read()

protected = content
protected = re.sub(r'&#x[\da-fA-F]+;', '\u0000HEX\u0000', protected)
protected = re.sub(r'&#\d+;', '\u0000DEC\u0000', protected)
protected = re.sub(r'&', '\u0000AMP\u0000', protected)
protected = re.sub(r'<', '\u0000LT\u0000', protected)
protected = re.sub(r'>', '\u0000GT\u0000', protected)
protected = re.sub(r'"', '\u0000QUOT\u0000', protected)
protected = re.sub(r'&apos;', '\u0000APOS\u0000', protected)

protected = protected.replace('&', '&')

protected = protected.replace('\u0000HEX\u0000', '&#x')
protected = protected.replace('\u0000DEC\u0000', '&#')
protected = protected.replace('\u0000AMP\u0000', '&')
protected = protected.replace('\u0000LT\u0000', '<')
protected = protected.replace('\u0000GT\u0000', '>')
protected = protected.replace('\u0000QUOT\u0000', '"')
protected = protected.replace('\u0000APOS\u0000', '&apos;')

with open('Views/MainPage.xaml', 'w') as f:
    f.write(protected)

print('Fixed MainPage.xaml')