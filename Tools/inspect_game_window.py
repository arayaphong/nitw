"""Inspect/focus a named local X11 game window; optionally send one key."""
import argparse
import ctypes as c
import json
import time

parser = argparse.ArgumentParser()
parser.add_argument('--title', required=True)
parser.add_argument('--focus', action='store_true')
parser.add_argument('--key')
args = parser.parse_args()
x = c.CDLL('libX11.so.6')
U, I, P = c.c_ulong, c.c_int, c.c_void_p
x.XOpenDisplay.argtypes = [c.c_char_p]
x.XOpenDisplay.restype = P
x.XDefaultRootWindow.argtypes = [P]
x.XDefaultRootWindow.restype = U
x.XInternAtom.argtypes = [P, c.c_char_p, I]
x.XInternAtom.restype = U
x.XGetWindowProperty.argtypes = [P,U,U,c.c_long,c.c_long,I,U,c.POINTER(U),c.POINTER(I),c.POINTER(U),c.POINTER(U),c.POINTER(c.POINTER(c.c_ubyte))]
x.XFetchName.argtypes = [P,U,c.POINTER(c.c_char_p)]
x.XGetGeometry.argtypes = [P,U,c.POINTER(U),c.POINTER(I),c.POINTER(I),c.POINTER(c.c_uint),c.POINTER(c.c_uint),c.POINTER(c.c_uint),c.POINTER(c.c_uint)]
x.XRaiseWindow.argtypes = [P,U]
x.XSetInputFocus.argtypes = [P,U,I,U]
x.XFlush.argtypes = [P]
x.XFree.argtypes = [P]
x.XCloseDisplay.argtypes = [P]
d = x.XOpenDisplay(None)
if not d:
    raise RuntimeError('No X display')

def prop(window, name):
    typ, fmt, count, remaining = U(), I(), U(), U()
    data = c.POINTER(c.c_ubyte)()
    atom = x.XInternAtom(d, name.encode(), 0)
    x.XGetWindowProperty(d,window,atom,0,4096,0,0,c.byref(typ),c.byref(fmt),c.byref(count),c.byref(remaining),c.byref(data))
    result = list(c.cast(data,c.POINTER(U))[:count.value]) if fmt.value == 32 and data else []
    if data:
        x.XFree(data)
    return result

found = []
for window in prop(x.XDefaultRootWindow(d), '_NET_CLIENT_LIST'):
    name = c.c_char_p()
    x.XFetchName(d, window, c.byref(name))
    title = name.value.decode(errors='replace') if name.value else ''
    if name:
        x.XFree(name)
    if args.title != title:
        continue
    root, px, py = U(), I(), I()
    width, height, border, depth = (c.c_uint() for _ in range(4))
    x.XGetGeometry(d,window,c.byref(root),c.byref(px),c.byref(py),c.byref(width),c.byref(height),c.byref(border),c.byref(depth))
    found.append(dict(window=hex(window), title=title, width=width.value, height=height.value,
                      fullscreen=x.XInternAtom(d,b'_NET_WM_STATE_FULLSCREEN',0) in prop(window,'_NET_WM_STATE')))
if len(found) != 1:
    raise RuntimeError(f'Expected one matching window, got {found}')
window = int(found[0]['window'], 16)
if args.focus:
    x.XRaiseWindow(d, window)
    x.XSetInputFocus(d, window, 2, 0)
    x.XFlush(d)
if args.key:
    if not args.focus:
        raise RuntimeError('Key injection requires explicit focus')
    x.XStringToKeysym.argtypes = [c.c_char_p]
    x.XStringToKeysym.restype = U
    x.XKeysymToKeycode.argtypes = [P,U]
    x.XKeysymToKeycode.restype = c.c_uint
    xt = c.CDLL('libXtst.so.6')
    xt.XTestFakeKeyEvent.argtypes = [P,c.c_uint,I,U]
    code = x.XKeysymToKeycode(d, x.XStringToKeysym(args.key.encode()))
    if not code:
        raise RuntimeError('Unknown key')
    xt.XTestFakeKeyEvent(d, code, 1, 0)
    x.XFlush(d)
    time.sleep(.08)
    xt.XTestFakeKeyEvent(d, code, 0, 0)
    x.XFlush(d)
x.XCloseDisplay(d)
print(json.dumps(found[0]))
