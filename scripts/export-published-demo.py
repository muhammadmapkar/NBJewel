#!/usr/bin/env python3
# Role: Preserves a public sample product plus its local original/preview before stopping the memory-only API.
# Does not read credentials, authenticate, mutate the API, or connect to SQL.
import argparse,datetime,hashlib,json,re,shutil,urllib.parse,urllib.request
from pathlib import Path
ROOT=Path(__file__).resolve().parent.parent

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--code',required=True);parser.add_argument('--api',default='http://localhost:5155')
    parser.add_argument('--assets',type=Path,default=ROOT/'backend/NbJewel.Api/App_Data/local-sample-demo/assets');args=parser.parse_args()
    base=urllib.parse.urlsplit(args.api)
    if base.scheme!='http' or base.hostname not in ('localhost','127.0.0.1') or base.username or base.password or base.query or base.fragment or base.path not in ('','/'):raise ValueError('Use a loopback API origin.')
    op=urllib.request.build_opener(urllib.request.ProxyHandler({}));found=None;page=1
    while True:
        with op.open(args.api.rstrip('/')+'/api/storefront/products?'+urllib.parse.urlencode({'search':args.code,'page':page,'pageSize':100}),timeout=10) as response:data=json.load(response)
        found=next((x for x in data['items'] if x['code']==args.code),None)
        if found or page*100>=data['totalCount']:break
        page+=1
    if not found:raise ValueError('Published product code was not found; keep the API running and export via authenticated ERP instead.')
    revision=urllib.parse.parse_qs(urllib.parse.urlsplit(found['previewUrl']).query).get('v',[''])[0]
    if not re.fullmatch('[0-9a-f]{32}',revision):raise ValueError('Invalid preview revision.')
    assets=args.assets.resolve();folder=assets/'revisions'/revision
    sources=list(folder.glob('source.*'))
    if len(sources)!=1 or not (folder/'preview.png').is_file():raise ValueError('Original source/preview not found; do not stop the API.')
    output=ROOT/'backend/NbJewel.Api/App_Data/sql-migration'/datetime.datetime.now().strftime('%Y%m%d-%H%M%S-%f')
    output.mkdir(parents=True,exist_ok=False)
    for file in folder.iterdir():
        if file.is_file():shutil.copy2(file,output/file.name)
    link=assets/'links'/f"item-{found['id']}.json"
    if link.exists():shutil.copy2(link,output/'original-asset.json')
    (output/'public-product.json').write_text(json.dumps(found,indent=2)+'\n')
    p=found['price'];meta=found['storefront'];image=output/sources[0].name
    row={'code':found['code'],'name':found['name'],'category':'Necklaces' if found['category']=='necklace' else 'Earrings','collection':meta['collection'],'description':meta['description'],'storefront':meta,'image':str(image.relative_to(ROOT)),'sha256':hashlib.sha256(image.read_bytes()).hexdigest(),'targetStatus':'Draft','isPublicDomain':False,'license':'Existing owner photo; licensing unresolved. Review again after SQL import.','grossWt':found['grossWeight'],'stoneWt':round(found['grossWeight']-found['netWeight'],3),'metalType':found['metalType'],'purity':found['purity'],'makingType':p['makingType'],'makingValue':p['makingValue'],'stoneValue':p['stoneValue'],'vatPercent':p['vatPercent']}
    (output/'import.json').write_text(json.dumps({'_role':'Preserved public QA item and images. SQL identity is newly assigned; manual approval/publication must be repeated. Private ERP fields/stock are not included.','products':[row]},indent=2)+'\n')
    print('Preserved public product, original image and preview at',output)
    print('Keep the original sample API running until any additional private ERP fields/stock are exported.')
if __name__=='__main__':main()
