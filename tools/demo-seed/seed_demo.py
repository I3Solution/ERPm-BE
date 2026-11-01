"""Loads a fictional demo company into a running ERPm API, using only the public endpoints the screens use.

Creates: one company under the group root with two branches and a store, a department tree, four job positions
(with default roles), a department role grant, five employees, two accounts and three items.

Usage (Python 3.9+, standard library only):
    python seed_demo.py --url http://127.0.0.1:5143 --out demo-credentials.local.json
Passwords come from the environment so they never appear in a command line or in git:
    ERPM_ADMIN_PASSWORD   password of the existing `admin` account (the bootstrap administrator)
    ERPM_DEMO_PASSWORD    password given to the demo accounts (optional; generated when missing)
Run it once against an empty database. A second run stops before changing anything.
"""
import argparse
import json
import os
import secrets
import sys
import urllib.error
import urllib.request


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--url', default='http://127.0.0.1:5143', help='API base URL (default: %(default)s)')
    parser.add_argument('--admin-user', default='admin', help='administrator user name (default: %(default)s)')
    parser.add_argument('--out', help='write the generated demo account list here (name it *.local.json: git ignores it)')
    args = parser.parse_args()

    admin_password = os.environ.get('ERPM_ADMIN_PASSWORD')
    if not admin_password:
        sys.exit('Set ERPM_ADMIN_PASSWORD to the administrator password.')
    demo_password = os.environ.get('ERPM_DEMO_PASSWORD') or 'Demo-' + secrets.token_hex(8)

    api = Api(args.url.rstrip('/'))
    api.login(args.admin_user, admin_password)
    seed(api, demo_password)

    if args.out:
        with open(args.out, 'w', encoding='utf-8') as f:
            json.dump({
                'note': 'Demo data only. Do not commit.',
                'url': args.url,
                'accounts': [
                    {'userName': args.admin_user, 'about': 'Super administrator (password: the one you provided)'},
                    {'userName': 'ketoan', 'password': demo_password, 'about': 'No system role; permissions come from the job position (sales accountant role, whole company)'},
                    {'userName': 'banhang', 'password': demo_password, 'about': 'No system role; permissions come from the department Phòng Kinh doanh (sales staff role)'},
                ]}, f, ensure_ascii=False, indent=2)
        print('accounts written to', args.out)
    elif not os.environ.get('ERPM_DEMO_PASSWORD'):
        print('demo account password (generated, shown once):', demo_password)
    print('seeded: 1 company, 4 branches, 4 departments, 4 positions, 5 employees, 2 accounts, 3 items')


class Api:
    def __init__(self, base: str) -> None:
        self.base = base
        self.token = ''

    def call(self, method: str, path: str, body=None):
        request = urllib.request.Request(self.base + '/api/v1' + path, method=method,
                                         data=None if body is None else json.dumps(body).encode('utf-8'))
        request.add_header('Content-Type', 'application/json')
        if self.token:
            request.add_header('Authorization', 'Bearer ' + self.token)
        try:
            with urllib.request.urlopen(request) as response:
                return json.loads(response.read() or b'{}')
        except urllib.error.HTTPError as error:
            sys.exit(f'{method} {path} -> {error.code}: {error.read().decode("utf-8", "replace")[:300]}')
        except urllib.error.URLError as error:
            sys.exit(f'cannot reach {self.base}: {error.reason}')

    def login(self, user: str, password: str) -> None:
        self.token = self.call('POST', '/auth/login', {'userName': user, 'password': password})['accessToken']

    def get(self, path: str):
        return self.call('GET', path)['data']

    def post(self, path: str, body):
        return self.call('POST', path, body)['data']

    def put(self, path: str, body):
        return self.call('PUT', path, body)['data']


def seed(api: Api, demo_password: str) -> None:
    branches = api.get('/branches')
    if any(b['code'] == 'COMPANY_A' for b in branches):
        sys.exit('Company A already exists: this database is already seeded. Nothing was changed.')
    group = next(b for b in branches if b['parentId'] is None)

    def branch(code, name, parent, kind, tax=None):
        return api.post('/branches', {'code': code, 'name': name, 'parentId': parent, 'orgUnitType': kind, 'taxCode': tax})

    company = branch('COMPANY_A', 'Công ty A', group['id'], 1, '0100000001')
    hanoi = branch('CA_HN', 'Chi nhánh Hà Nội', company['id'], 2)
    hcm = branch('CA_HCM', 'Chi nhánh TP.HCM', company['id'], 2)
    branch('CA_HN_01', 'Cửa hàng Hà Nội 01', hanoi['id'], 2)

    levels = {l['code']: l['id'] for l in api.get('/hrm/department-levels?pageSize=50')['items']}

    def department(code, name, level, parent=None):
        return api.post('/hrm/departments', {'code': code, 'name': name, 'companyId': company['id'], 'branchId': hanoi['id'],
                                             'parentId': parent, 'levelId': levels[level], 'isActive': True})

    finance = department('KTC', 'Khối Tài chính', 'DIVISION')
    accounting = department('KT', 'Phòng Kế toán', 'DEPARTMENT', finance['id'])
    department('CN', 'Bộ phận Công nợ', 'SECTION', accounting['id'])
    sales_dept = department('KD', 'Phòng Kinh doanh', 'DEPARTMENT')

    roles = {r['code']: r['id'] for r in api.get('/sys/roles')}

    def position(code, name, rank, scope, department_id, role_code):
        created = api.post('/hrm/job-positions', {'code': code, 'name': name, 'companyId': company['id'], 'departmentId': department_id,
                                                  'rankOrder': rank, 'defaultScopeMode': scope, 'isActive': True})
        if role_code:
            api.put(f"/sys/roles/job-positions/{created['id']}", {'roleIds': [roles[role_code]]})
        return created

    chief = position('KTT', 'Kế toán trưởng', 2, 'COMPANY', accounting['id'], 'SALES_ACCOUNTANT')
    accountant = position('KTBH', 'Kế toán bán hàng', 4, 'BRANCH_TREE', accounting['id'], 'SALES_ACCOUNTANT')
    director = position('GDCN', 'Giám đốc chi nhánh', 1, 'BRANCH_TREE', None, 'BRANCH_DIRECTOR')
    seller = position('NVBH', 'Nhân viên bán hàng', 5, 'SELF', sales_dept['id'], None)

    # Everyone in Phòng Kinh doanh (and its sub-units) gets the sales staff role from the department.
    api.put(f"/sys/roles/departments/{sales_dept['id']}", [{'roleId': roles['SALES_STAFF'], 'applyToChildren': True}])

    def employee(code, name, pos, branch_id, department_id=None):
        return api.post('/hrm/employees', {'code': code, 'fullName': name, 'companyId': company['id'], 'branchId': branch_id,
                                           'departmentId': department_id, 'jobPositionId': pos['id'], 'hireDate': '2026-01-05'})

    first = employee('NV001', 'Nguyễn Văn An', chief, hanoi['id'], accounting['id'])
    employee('NV002', 'Trần Thị Bình', accountant, hanoi['id'], accounting['id'])
    employee('NV003', 'Lê Văn Cường', director, hanoi['id'])
    fourth = employee('NV004', 'Phạm Thị Dung', seller, hanoi['id'], sales_dept['id'])
    employee('NV005', 'Hoàng Văn Em', seller, hcm['id'])

    for user, display, emp in (('ketoan', 'Nguyễn Văn An (Kế toán trưởng)', first), ('banhang', 'Phạm Thị Dung (Bán hàng)', fourth)):
        api.post('/auth/users', {'userName': user, 'displayName': display, 'password': demo_password, 'role': 'None',
                                 'branchIds': [], 'employeeId': emp['id']})

    uom = api.get('/wms/units-of-measure')['items'][0]['id']
    for code, name, price in (('SP001', 'Bút bi', 5000), ('SP002', 'Vở 200 trang', 12000), ('SP003', 'Giấy A4 (ram)', 70000)):
        api.post('/items', {'code': code, 'name': name, 'itemType': 1, 'baseUomId': uom, 'salePrice': price, 'costPrice': round(price * 0.6),
                            'minStockAlert': 0, 'maxStockAlert': 0})


if __name__ == '__main__':
    main()
