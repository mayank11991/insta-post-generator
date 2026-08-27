# Fix AboutPage.xaml - comprehensive approach
with open('Views/AboutPage.xaml', 'r') as f:
    content = f.read()

# Replace ALL literal ampersands that are NOT part of valid XML entities
# Valid entities: &#xNNNN; &#NNNN; & < > " &apos;
# Strategy: iterate through and replace & that don't start a valid entity

result = []
i = 0
while i < len(content):
    if content[i] == '&':
        # Check if this starts a valid entity
        is_entity = False
        if i + 1 < len(content):
            if content[i+1] == '#':
                # Numeric entity: &#x...; or &#...;
                j = i + 2
                if j < len(content) and content[j] in 'xX':
                    j += 1
                while j < len(content) and content[j].isdigit():
                    j += 1
                if j < len(content) and content[j] == ';':
                    is_entity = True
            else:
                # Named entity
                for entity in ['amp', 'lt', 'gt', 'quot', 'apos']:
                    if content.startswith(entity + ';', i+1):
                        is_entity = True
                        break
        
        if is_entity:
            result.append('&')
        else:
            result.append('&')
        i += 1
    else:
        result.append(content[i])
        i += 1

with open('Views/AboutPage.xaml', 'w') as f:
    f.write(''.join(result))

print('Fixed AboutPage.xaml')

# Fix MainPage.xaml
with open('Views/MainPage.xaml', 'r') as f:
    content = f.read()

result = []
i = 0
while i < len(content):
    if content[i] == '&':
        is_entity = False
        if i + 1 < len(content):
            if content[i+1] == '#':
                j = i + 2
                if j < len(content) and content[j] in 'xX':
                    j += 1
                while j < len(content) and content[j].isdigit():
                    j += 1
                if j < len(content) and content[j] == ';':
                    is_entity = True
            else:
                for entity in ['amp', 'lt', 'gt', 'quot', 'apos']:
                    if content.startswith(entity + ';', i+1):
                        is_entity = True
                        break
        if is_entity:
            result.append('&')
        else:
            result.append('&')
        i += 1
    else:
        result.append(content[i])
        i += 1

with open('Views/MainPage.xaml', 'w') as f:
    f.write(''.join(result))

print('Fixed MainPage.xaml')