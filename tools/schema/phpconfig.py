"""Converts ebunny's config/*.php (plain array literals) to JSON. env('X', default) becomes its default;
Foo::class becomes "Foo"; closures and other expressions become null."""
import json, re, sys

def tokenize(s):
    i, n = 0, len(s)
    while i < n:
        c = s[i]
        if c in ' \t\r\n': i += 1; continue
        if s.startswith('//', i) or c == '#':
            j = s.find('\n', i); i = n if j < 0 else j; continue
        if s.startswith('/*', i):
            j = s.find('*/', i); i = j + 2; continue
        if c in "'\"":
            j, buf = i + 1, []
            while s[j] != c:
                if s[j] == '\\' and s[j+1] in (c, '\\'): buf.append(s[j+1]); j += 2; continue
                if c == '"' and s[j] == '\\' and s[j+1] == 'n': buf.append('\n'); j += 2; continue
                buf.append(s[j]); j += 1
            yield ('str', ''.join(buf)); i = j + 1; continue
        if s.startswith('=>', i): yield ('=>', None); i += 2; continue
        if c in '[](),.': yield (c, None); i += 1; continue
        m = re.match(r'-?\d+(\.\d+)?', s[i:])
        if m and (c.isdigit() or c == '-'):
            v = m.group(0); yield ('num', float(v) if '.' in v else int(v)); i += len(v); continue
        m = re.match(r'[\\A-Za-z_][\\A-Za-z_0-9]*(::[A-Za-z_]+)?', s[i:])
        if m: yield ('id', m.group(0)); i += len(m.group(0)); continue
        yield ('?', c); i += 1

class P:
    def __init__(self, toks): self.t = list(toks); self.i = 0
    def peek(self): return self.t[self.i] if self.i < len(self.t) else (None, None)
    def next(self): tok = self.peek(); self.i += 1; return tok
    def value(self):
        k, v = self.next()
        if k == '[': return self.array()
        if k == 'str':
            # string concatenation 'a'.'b'
            while self.peek()[0] == '.':
                self.next(); nv = self.value(); v = v + (nv if isinstance(nv, str) else str(nv if nv is not None else ''))
            return v
        if k == 'num': return v
        if k == 'id':
            low = v.lower()
            if low in ('true', 'false'): return low == 'true'
            if low == 'null': return None
            if v.endswith('::class'): return v[:-7].split('\\')[-1]
            if v in ('env',):
                self.next(); args = [];
                while self.peek()[0] != ')':
                    args.append(self.value())
                    if self.peek()[0] == ',': self.next()
                self.next(); return args[1] if len(args) > 1 else None
            if v in ('fn', 'function'):
                self.skip_expr(); return None
            # function call or constant: skip balanced
            if self.peek()[0] == '(':
                self.skip_parens()
                while self.peek()[0] in ('.',) : self.next(); self.value()
                return None
            return None
        if k == '(':
            self.i -= 1; self.skip_parens(); return None
        return None
    def skip_parens(self):
        depth = 0
        while True:
            k, _ = self.next()
            if k in ('(', '['): depth += 1
            elif k in (')', ']'):
                depth -= 1
                if depth == 0: return
            if k is None: return
    def skip_expr(self):
        depth = 0
        while True:
            k, _ = self.peek()
            if k is None: return
            if depth == 0 and k in (',', ']', ')'): return
            if k in ('(', '['): depth += 1
            if k in (')', ']'): depth -= 1
            self.next()
    def array(self):
        items, keyed = [], False
        while self.peek()[0] != ']':
            v = self.value()
            if self.peek()[0] == '=>':
                self.next(); keyed = True; items.append((v, self.value()))
            else:
                items.append((None, v))
            if self.peek()[0] == ',': self.next()
            elif self.peek()[0] != ']': self.skip_expr();
            if self.peek()[0] == ',': self.next()
        self.next()
        if keyed:
            out, auto = {}, 0
            for k, v in items:
                if k is None: k = auto
                if isinstance(k, int): auto = k + 1
                out[str(k)] = v
            return out
        return [v for _, v in items]

def convert(path):
    src = open(path).read()
    i = src.index('return [')
    p = P(tokenize(src[i + len('return '):]))
    p.next()
    return p.array()

if __name__ == '__main__':
    for path in sys.argv[2:]:
        name = path.split('/')[-1][:-4]
        json.dump(convert(path), open(f'{sys.argv[1]}/{name}.json', 'w'), indent=1, ensure_ascii=False)
        print('ok', name)
