#!/usr/bin/env python3
# Minimal texconv stand-in: PNG -> DXT1/DXT3/DXT5 DDS, one mip. DXT3 alpha is packed here
# (Pillow's DXT3 alpha is wrong); colour blocks come from Pillow's DXT5 encoder.
import sys, os, io, struct
import numpy as np
from PIL import Image
a=sys.argv[1:]; fmt='DXT3'; out='.'; inp=None; i=0
while i<len(a):
    x=a[i]
    if x in('-f','-o','-ft','-m'):
        if x=='-f': fmt=a[i+1].upper()
        if x=='-o': out=a[i+1]
        i+=2; continue
    if x.startswith('-'): i+=1; continue
    inp=x; i+=1
fmt={'BC1_UNORM':'DXT1','BC2_UNORM':'DXT3','BC3_UNORM':'DXT5'}.get(fmt,fmt)
im=Image.open(inp).convert('RGBA'); name=os.path.join(out,os.path.splitext(os.path.basename(inp))[0]+'.DDS')
if fmt!='DXT3':
    im.save(name,'DDS',pixel_format=fmt); sys.exit(0)
b=io.BytesIO(); im.save(b,'DDS',pixel_format='DXT5'); d=b.getvalue()
hdr=bytearray(d[:128]); body=d[128:]
W,H=im.size; A=np.asarray(im)[...,3].astype(np.float32)
q=np.clip(np.rint(A*15/255),0,15).astype(np.uint8)
outb=bytearray()
bi=0
for by in range(0,H,4):
    for bx in range(0,W,4):
        blk=q[by:by+4,bx:bx+4]
        al=bytearray()
        for r in range(4):
            v=0
            for c in range(4): v|=int(blk[r,c])<<(4*c)
            al+=struct.pack('<H',v)
        outb+=al+body[bi*16+8:bi*16+16]; bi+=1
hdr[84:88]=b'DXT3'
open(name,'wb').write(bytes(hdr)+bytes(outb))
