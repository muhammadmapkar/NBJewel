# Role: Tests demo import validation, no-overwrite behavior and SQL launcher isolation offline.
import importlib.util,json,tempfile,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
def module(name):
    spec=importlib.util.spec_from_file_location(name,ROOT/'scripts'/f'{name}.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m

class SqlDemoTests(unittest.TestCase):
    def test_manifest_images_are_verified_and_publication_is_manual(self):
        tool=module('import-erp-demo'); rows=tool.read_manifest(ROOT/'Jewellery Images/demo-import.json')
        new=[x for x in rows if x['code'].startswith('DEMO-MET-')]
        self.assertEqual(12,len(new)); self.assertEqual(8,sum(x['targetStatus']=='Complete' for x in new))
        self.assertTrue(all(x['isPublicDomain'] for x in new))
        self.assertTrue(all(x['targetStatus']=='Draft' for x in rows if not x['isPublicDomain']))
    def test_existing_code_is_never_overwritten(self):
        tool=module('import-erp-demo')
        class ExistingClient:
            def request(self,method,path,data=None):
                if method=='GET' and path.startswith('/api/items?'): return {'items':[{'id':91,'code':'DEMO-ONE','name':'Retailer edit'}],'page':1,'totalCount':1}
                raise AssertionError('Unexpected mutation or reference lookup: '+path)
        row={'code':'DEMO-ONE','name':'Importer original'}
        self.assertEqual('skipped',tool.import_one(ExistingClient(),row,None)['result'])
    def test_new_import_uses_erp_validation_without_auto_approval(self):
        tool=module('import-erp-demo');row=tool.read_manifest(ROOT/'Jewellery Images/demo-import.json')[0]
        calls=[]
        class NewClient:
            def request(self,method,path,data=None):
                calls.append((method,path,data))
                if path.startswith('/api/items?'):return {'items':[],'totalCount':0}
                if path=='/api/categories':return [{'id':4,'name':'Necklaces'}]
                if path=='/api/designs':return [{'designId':8,'designCode':'DEMO-HERITAGE'}]
                if method=='POST' and path=='/api/items':return {'id':123}
                if path in ('/api/items/123/stock','/api/items/123/status'):return {}
                raise AssertionError(path)
            def upload(self,item_id,path):calls.append(('UPLOAD',str(item_id),None));return {'state':'ready'}
        self.assertEqual(123,tool.import_one(NewClient(),row,2)['id'])
        creation=next(x[2] for x in calls if x[0]=='POST' and x[1]=='/api/items')
        self.assertEqual(4,creation['categoryId']);self.assertEqual(8,creation['designId'])
        self.assertTrue(creation['storefront']['isSample'])
        self.assertFalse(any('review' in x[1] or 'publish' in x[1] for x in calls))
    def test_manifest_rejects_modified_image(self):
        tool=module('import-erp-demo');data=json.loads((ROOT/'Jewellery Images/demo-import.json').read_text());data['products'][0]['sha256']='bad'
        with tempfile.TemporaryDirectory() as folder:
            path=Path(folder)/'manifest.json';path.write_text(json.dumps(data))
            with self.assertRaises(ValueError):tool.read_manifest(path)
    def test_sql_launcher_removes_sample_identity_and_requires_all_connections(self):
        launcher=module('run-sql-api')
        with self.assertRaises(ValueError): launcher.settings({})
        env=launcher.settings({k:'test-only' for k in ('NBJEWEL_SQL_CONNECTION','NBJEWEL_IDENTITY_CONNECTION','AI_PLATFORM_CONNECTION')}|{'SampleIdentity__Enabled':'true','SampleIdentity__StaffPassword':'not-a-real-password','ASPNETCORE_URLS':'http://0.0.0.0:99'})
        self.assertEqual('Sql',env['Repository__Provider']);self.assertEqual('false',env['SampleIdentity__Enabled'])
        self.assertNotIn('SampleIdentity__StaffPassword',env);self.assertEqual('http://localhost:5155',env['ASPNETCORE_URLS'])
    def test_prepared_schema_is_additive_and_transactional(self):
        sql=module('prepare-sql-demo').prepare()
        self.assertIn('ROLLBACK TRANSACTION',sql);self.assertNotIn('DROP TABLE',sql);self.assertNotIn('DELETE FROM',sql)
        for table in ['dbo.ItemImageAsset','dbo.AspNetUsers','dbo.CustomerPortals','dbo.AiPublicationProducts']: self.assertIn(table,sql)

if __name__=='__main__':unittest.main()
