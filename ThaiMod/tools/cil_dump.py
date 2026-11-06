"""Dump CIL of selected methods with resolved member/type/string names.

Usage: cil_dump.py <assembly.dll> <Type[.Method]> [...]
Static read only; the assembly is never loaded or executed.
"""
import sys

import dnfile
from dncil.cil.body import CilMethodBody
from dncil.cil.body.reader import CilMethodBodyReaderBytes
from dncil.cil.error import MethodBodyFormatError

pe = dnfile.dnPE(sys.argv[1])
md = pe.net.mdtables
types = md.TypeDef.rows
methods = md.MethodDef.rows
fields = md.Field.rows

def type_name(row):
    ns = str(row.TypeNamespace or '')
    return (ns + '.' if ns else '') + str(row.TypeName)

# Map method/field row index -> owning type (MethodList/FieldList ranges).
method_owner, field_owner = {}, {}
for i, t in enumerate(types):
    nxt = types[i + 1] if i + 1 < len(types) else None
    ms = t.MethodList[0].row_index if t.MethodList else None
    for m in t.MethodList:
        method_owner[m.row_index] = type_name(t)
    for f in t.FieldList:
        field_owner[f.row_index] = type_name(t)

def resolve(token):
    table, index = token >> 24, token & 0xFFFFFF
    try:
        if table == 0x70:
            return repr(pe.net.user_strings.get(index).value)
        if table == 0x06:
            return f'{method_owner.get(index, "?")}::{methods[index - 1].Name}'
        if table == 0x04:
            return f'{field_owner.get(index, "?")}::{fields[index - 1].Name}'
        if table == 0x0A:
            r = md.MemberRef.rows[index - 1]
            parent = r.Class.row
            owner = getattr(parent, 'TypeName', None)
            owner = (str(parent.TypeNamespace) + '.' + str(owner)) if owner is not None else '?'
            return f'{owner}::{r.Name}'
        if table == 0x02:
            return type_name(types[index - 1])
        if table == 0x01:
            r = md.TypeRef.rows[index - 1]
            return f'{r.TypeNamespace}.{r.TypeName}'
        if table == 0x2B:
            r = md.MethodSpec.rows[index - 1]
            return 'spec:' + resolve(r.Method.table_index_token if hasattr(r.Method, 'table_index_token') else 0)
    except Exception as e:  # keep dumping even when a row is unusual
        return f'<{token:#x} {e}>'
    return f'<{token:#x}>'

def dump(index):
    m = methods[index - 1]
    rva = m.Rva
    if not rva:
        print('  (no body)'); return
    data = pe.get_data(rva, 0x10000)
    try:
        body = CilMethodBody(CilMethodBodyReaderBytes(data))
    except MethodBodyFormatError as e:
        print('  body error', e); return
    for insn in body.instructions:
        operand = insn.operand
        text = ''
        if operand is not None:
            tok = getattr(operand, 'value', operand)
            if hasattr(operand, 'table') or (isinstance(tok, int) and insn.opcode.operand_type.name.startswith('Inline') and
                                              insn.opcode.operand_type.name in ('InlineMethod', 'InlineField', 'InlineType', 'InlineTok', 'InlineString')):
                text = resolve(int(tok))
            else:
                text = str(operand)
        print(f'  IL_{insn.offset:04x} {insn.opcode.name:12s} {text}')

for spec in sys.argv[2:]:
    tname, _, mname = spec.partition('::')
    for i, t in enumerate(types):
        if type_name(t) != tname:
            continue
        for m in t.MethodList:
            if mname and str(methods[m.row_index - 1].Name) != mname:
                continue
            print(f'== {tname}::{methods[m.row_index - 1].Name}')
            dump(m.row_index)
