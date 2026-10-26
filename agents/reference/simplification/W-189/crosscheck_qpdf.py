import json,subprocess,csv,sys,collections
sys.path.insert(0,'.')
PW={"libreoffice-memo-rc4.pdf":"corpus-user","qpdf-encrypted.pdf":"user-secret"}
feat={x['path']:x for x in json.load(open('agents/reference/simplification/W-189/features.json'))}
keys={"/StructTreeRoot":"tagged","/Outlines":"outlines","/PageLabels":"page-labels","/XFA":"xfa","/UR3":"usage-rights","/EmbeddedFiles":"embedded-files","/OCProperties":"optional-content","/Lock":"field-lock"}
diff=collections.defaultdict(list)
for p,x in feat.items():
    n=p.split('/')[-1]
    pw=PW.get(n) or ("user-secret" if n.startswith("pypdf-unsigned") else None)
    a=['.tools/msys64/ucrt64/bin/qpdf.exe'] if False else ['qpdf']
    import glob
    q=glob.glob('.tools/**/qpdf.exe',recursive=True)[0]
    cmd=[q]+(['--password='+pw] if pw else [])+['--json=2','--json-key=qpdf',p]
    out=subprocess.run(cmd,capture_output=True).stdout.decode('utf-8','replace')
    for k,f in keys.items():
        q_has = ('"'+k+'"') in out
        if q_has != (f in x['features']) and not x['errors']:
            diff[f].append((n,'qpdf' if q_has else 'pypdf-only'))
print(dict(diff))
