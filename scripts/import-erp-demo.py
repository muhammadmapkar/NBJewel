#!/usr/bin/env python3
# Role: Validates/imports sample products through authenticated ERP APIs. Defaults to offline dry-run.
# Never approves/publishes images, deletes records, or replaces any existing item code.
import argparse,getpass,hashlib,http.cookiejar,json,mimetypes,sys,urllib.error,urllib.parse,urllib.request,uuid
from pathlib import Path
ROOT=Path(__file__).resolve().parent.parent

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self,*args,**kwargs): raise ValueError('Redirect refused; check the local API address.')

class Client:
    def __init__(self,base):
        parsed=urllib.parse.urlsplit(base)
        if parsed.scheme!='http' or parsed.hostname not in ('localhost','127.0.0.1') or parsed.username or parsed.password or parsed.query or parsed.fragment or parsed.path not in ('','/'):
            raise ValueError('Use the loopback HTTP API, for example http://localhost:5155.')
        self.base=base.rstrip('/');self.token=None
        self.opener=urllib.request.build_opener(urllib.request.ProxyHandler({}),NoRedirect(),urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
    def request(self,method,path,data=None):
        payload=None if data is None else json.dumps(data).encode()
        headers={'Content-Type':'application/json'}
        if self.token: headers['X-CSRF-TOKEN']=self.token
        with self.opener.open(urllib.request.Request(self.base+path,payload,headers,method=method),timeout=90) as response:
            body=response.read();return json.loads(body) if body else None
    def login(self):
        self.token=self.request('GET','/api/auth/csrf')['token']
        self.request('POST','/api/auth/login',{'email':input('ERP email: ').strip(),'password':getpass.getpass('ERP password: ')})
        self.token=self.request('GET','/api/auth/csrf')['token']
    def upload(self,item_id,path):
        boundary='nbjewel'+uuid.uuid4().hex
        mime=mimetypes.guess_type(path.name)[0] or 'application/octet-stream'
        body=(f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="upload{path.suffix}"\r\nContent-Type: {mime}\r\n\r\n').encode()+path.read_bytes()+f'\r\n--{boundary}--\r\n'.encode()
        headers={'Content-Type':'multipart/form-data; boundary='+boundary,'X-CSRF-TOKEN':self.token}
        with self.opener.open(urllib.request.Request(self.base+f'/api/items/{item_id}/image',body,headers,method='POST'),timeout=90) as r:return json.load(r)

def read_manifest(path):
    rows=json.loads(Path(path).read_text())['products'];seen=set()
    for row in rows:
        code=row['code']
        if not isinstance(code,str) or not code or len(code)>30 or code.casefold() in seen:raise ValueError('Invalid/duplicate product code.')
        seen.add(code.casefold())
        image=(ROOT/row['image']).resolve()
        if not image.is_relative_to(ROOT) or not image.is_file():raise ValueError('Image must exist inside the repository.')
        data=image.read_bytes()
        if len(data)>5*1024*1024 or not (data.startswith(b'\x89PNG\r\n\x1a\n') or data.startswith(b'\xff\xd8\xff')):raise ValueError('Upload must be PNG/JPG <=5 MB.')
        if hashlib.sha256(data).hexdigest()!=row['sha256']:raise ValueError('Image checksum mismatch: '+code)
        if row['targetStatus'] not in ('Draft','Complete') or row['category'] not in ('Necklaces','Earrings'):raise ValueError('Unsupported import status/category.')
        if not row.get('isPublicDomain') and row['targetStatus']!='Draft':raise ValueError('Unresolved existing images must remain Draft.')
        if not row.get('description'):raise ValueError('Description required.')
    return rows

def import_one(client,row,store_id):
    page=1
    while True:
        result=client.request('GET','/api/items?'+urllib.parse.urlencode({'search':row['code'],'page':page,'pageSize':100}))
        existing=next((x for x in result['items'] if x['code'].casefold()==row['code'].casefold()),None)
        if existing:return {'code':row['code'],'id':existing['id'],'result':'skipped'}
        if page*100>=result['totalCount']:break
        if not result['items']:raise ValueError('Incomplete item search.')
        page+=1
    categories=client.request('GET','/api/categories');designs=client.request('GET','/api/designs')
    category=next((x for x in categories if x['name']==row['category']),None)
    design=next((x for x in designs if x['designCode']==row.get('designCode','DEMO-HERITAGE')),None)
    if not category or not design:raise ValueError('Missing category/design reference data; apply reviewed demo references first.')
    request={key:row[key] for key in ('code','name','metalType','purity','grossWt','stoneWt','makingType','makingValue','stoneValue','vatPercent')}
    request.update(categoryId=category['id'],designId=design['designId'],netWt=row['grossWt']-row['stoneWt'],huid=row.get('huid'),imageUrl=None,
                   storefront=row.get('storefront') or {'description':row['description'],'collection':row['collection'],'note':'Assumed sample values for demonstration','isSample':True,'occasions':[],'stones':[],'popularity':1,'badge':'new'})
    created=client.request('POST','/api/items',request);item_id=created['id']
    # Report the durable ID before later steps. A partial import stays Draft and is never overwritten on retry.
    print('Created',row['code'],'item',item_id,flush=True)
    client.upload(item_id,ROOT/row['image'])
    if store_id is not None:client.request('POST',f'/api/items/{item_id}/stock',{'variantId':None,'storeId':store_id,'quantity':1})
    if row['targetStatus']=='Complete':client.request('PATCH',f'/api/items/{item_id}/status',{'status':'Complete'})
    return {'code':row['code'],'id':item_id,'result':'created; manual preview review/publication required'}

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest',type=Path,default=ROOT/'Jewellery Images/demo-import.json')
    parser.add_argument('--api',default='http://localhost:5155');parser.add_argument('--apply',action='store_true')
    parser.add_argument('--store-id',type=int,help='Optional explicit ERP store for one assumed sample unit per NEW item')
    args=parser.parse_args();rows=read_manifest(args.manifest)
    if not args.apply:
        for row in rows:print(row['code'],row['category'],row['targetStatus'])
        print(f'Validated {len(rows)} products. Offline dry-run; no API/SQL writes.');return
    client=Client(args.api);client.login()
    if client.request('GET','/api/items/storage')['provider']!='Sql':raise ValueError('Import refused: API is not using SQL. Preserve/export sample work before changing launchers.')
    if args.store_id is not None and not any(x['id']==args.store_id for x in client.request('GET','/api/stores')):raise ValueError('Unknown store ID.')
    failed=False
    for row in rows:
        try: print(json.dumps(import_one(client,row,args.store_id)))
        except (urllib.error.HTTPError,ValueError,OSError) as error:
            failed=True;print(row['code'],'incomplete:', ('HTTP '+str(error.code)) if isinstance(error,urllib.error.HTTPError) else type(error).__name__, '; inspect ERP before retrying.',file=sys.stderr)
    if failed:sys.exit(1)

if __name__=='__main__':
    try:main()
    except urllib.error.HTTPError as error:sys.exit('API request failed: HTTP '+str(error.code)+'. No credentials printed.')
    except (ValueError,OSError,KeyError) as error:sys.exit(str(error))
