import re, os, json, sys
import sys
MIG = sys.argv[1] if len(sys.argv) > 1 else '../../../ebunny/database/migrations'
tables = {}   # name -> {cols: ordered dict, pk, indexes, uniques}
order = []

def up_body(src):
    i = src.find('function up')
    if i < 0: return ''
    j = src.find('function down', i)
    return src[i:j if j > 0 else len(src)]

def blocks(body):
    # yields (kind, table, inner) for Schema::create / Schema::table
    for m in re.finditer(r"Schema::(create|table)\(\s*'([a-z_0-9]+)'\s*,\s*(?:function\s*\([^)]*\)\s*(?::\s*\w+\s*)?\{|fn\s*\([^)]*\)\s*=>)", body):
        start = m.end()
        if body[m.end()-1] == '{':
            depth, k = 1, start
            while depth and k < len(body):
                if body[k] == '{': depth += 1
                elif body[k] == '}': depth -= 1
                k += 1
            inner = body[start:k-1]
        else:
            # arrow fn: until matching ');'
            depth, k = 0, start
            while k < len(body):
                c = body[k]
                if c in '([': depth += 1
                elif c in ')]':
                    if depth == 0: break
                    depth -= 1
                k += 1
            inner = body[start:k]
        yield m.group(1), m.group(2), inner

def stmts(inner):
    # split into statements beginning with $table-> or $t->
    parts = re.split(r';', inner)
    for p in parts:
        p = p.strip()
        p = re.sub(r'//[^\n]*', '', p)
        m = re.search(r"\$(table|t)->", p)
        if m:
            yield p[m.start():]

def args(s):
    return re.findall(r"'([^']*)'|(-?\d+(?:\.\d+)?)|(true|false|null)|\[([^\]]*)\]", s)

TYPEMAP = {'string':'string','text':'text','longText':'text','mediumText':'text','json':'json','boolean':'bool',
 'integer':'int','unsignedInteger':'int','unsignedSmallInteger':'int','unsignedTinyInteger':'int','bigInteger':'long','unsignedBigInteger':'long',
 'decimal':'decimal','date':'date','timestamp':'datetime','time':'time','foreignId':'fk'}

for f in sorted(os.listdir(MIG)):
    src = open(os.path.join(MIG, f)).read()
    body = up_body(src)
    for kind, tname, inner in blocks(body):
        if kind == 'create':
            tables[tname] = {'cols': {}, 'pk': None, 'indexes': [], 'uniques': [], 'file': f}
            order.append(tname)
        t = tables.setdefault(tname, {'cols': {}, 'pk': None, 'indexes': [], 'uniques': [], 'file': f})
        for s in stmts(inner):
            m = re.match(r"\$(?:table|t)->(\w+)\((.*?)\)(.*)$", s, re.S)
            if not m: continue
            fn, a, rest = m.group(1), m.group(2), m.group(3)
            strs = re.findall(r"'([^']*)'", a)
            if fn == 'id':
                t['cols']['id'] = {'type': 'pk', 'nullable': False}; t['pk'] = ['id']; continue
            if fn == 'timestamps':
                for c in ('created_at', 'updated_at'): t['cols'][c] = {'type': 'datetime', 'nullable': True}
                continue
            if fn == 'rememberToken':
                t['cols']['remember_token'] = {'type': 'string', 'nullable': True}; continue
            if fn == 'dropColumn':
                for c in re.findall(r"'([^']*)'", a): t['cols'].pop(c, None)
                continue
            if fn == 'dropConstrainedForeignId':
                t['cols'].pop(strs[0], None); continue
            if fn in ('dropUnique', 'dropIndex'):
                continue
            if fn == 'primary':
                t['pk'] = re.findall(r"'([^']*)'", a); continue
            if fn in ('index', 'unique'):
                cols = re.findall(r"'([^']*)'", a.split(']')[0] if '[' in a else a)
                if '[' not in a: cols = cols[:1]
                (t['uniques'] if fn == 'unique' else t['indexes']).append(cols); continue
            if fn in TYPEMAP and strs:
                name = strs[0]
                col = {'type': TYPEMAP[fn], 'nullable': '->nullable()' in rest, 'php': fn}
                nums = re.findall(r",\s*(\d+)", a)
                if fn == 'string' and nums: col['len'] = int(nums[0])
                if fn == 'decimal' and len(nums) >= 2: col['prec'] = (int(nums[0]), int(nums[1]))
                d = re.search(r"->default\((.*?)\)(?:->|$)", rest)
                if d: col['default'] = d.group(1).strip()
                if '->unique()' in rest: t['uniques'].append([name])
                if '->primary()' in rest: t['pk'] = [name]
                if re.search(r'->index\(\)', rest): t['indexes'].append([name])
                if fn == 'foreignId':
                    c = re.search(r"->constrained\(\s*(?:'([^']*)')?", rest)
                    if c:
                        base = name[:-3]
                        ref = c.group(1) or (base[:-1] + 'ies' if base.endswith('y') and base[-2] not in 'aeiou' else base + 's')
                        if ref.endswith('ys') and not ref.endswith('ays'): pass
                        col['ref'] = ref
                        col['onDelete'] = 'cascade' if 'cascadeOnDelete' in rest else 'setnull' if 'nullOnDelete' in rest else 'restrict'
                if '->change()' in rest and name in t['cols']:
                    t['cols'][name].update(col); continue
                t['cols'][name] = col
                continue
            print('UNHANDLED', f, tname, fn, file=sys.stderr)

json.dump({'order': order, 'tables': tables}, open('schema.json', 'w'), indent=1)
print(len(order), 'tables')

# 2026_10_05_000002 builds these columns in a loop (content_<area>_id) and drops the old HTML columns.
_p = json.load(open('schema.json'))
pages = _p['tables']['pages']['cols']
pages.pop('content_', None)
for area in ('primary', 'secondary', 'parent', 'auxiliary', 'amp'):
    pages.pop('content_' + area, None)
    pages['content_' + area + '_id'] = {'type': 'fk', 'nullable': True, 'ref': 'content_blocks', 'onDelete': 'setnull', 'php': 'foreignId'}
json.dump(_p, open('schema.json', 'w'), indent=1)
