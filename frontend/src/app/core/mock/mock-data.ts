// Role: Holds all static fake values used by the visual-only Angular prototype.

export type MakingType = 'perGram' | 'percent' | 'flat';
export interface MenuItem { label: string; icon: string; children?: string[]; route?: string; }
export interface Widget { title: string; value?: string; kind: 'kpi' | 'chart' | 'table' | 'donut'; visible: boolean; }

export const menuItems: MenuItem[] = [
  { label: 'Gold Scheme', icon: '▣', children: ['Masters', 'Chit Discount Setup', 'Registration Master list', 'Scheme Master', 'Chit Master', 'Transactions', 'Reports'] },
  { label: 'Finance', icon: '◉' }, { label: 'Inventory', icon: '▣', children: ['Item Catalog'] }, { label: 'Purchase', icon: '◇' },
  { label: 'Sales', icon: '▤' }, { label: 'General', icon: '⠿' },
  { label: 'Change Financial Year', icon: '▦', route: '/change-financial-year' },
  { label: 'Change Password', icon: '⚿', route: '/change-password' }, { label: 'Other Settings', icon: '⚙' }
];

export const rates = ['DIAMOND (18K): 13,000', 'DIAMOND (22K): 14,000', 'GOLD (18K): 13,000', 'GOLD (22K): 14,000', 'PLATINUM (950): 3,650'];
export const years = ['2025 - 2026', '2024 - 2025', '2023 - 2024', '2022 - 2023', '2021 - 2022', '2020 - 2021', '2019 - 2020', '2018 - 2019'];
export const saleBars = [
  { label: 'BANGLE', value: 69.539, color: '#a71932' }, { label: 'CHAIN', value: 135.97, color: '#dc3b2d' },
  { label: 'DIAMOND', value: 142.08, color: '#f26d43' }, { label: 'DOLLAR', value: 10.87, color: '#ffb55e' },
  { label: 'GENERAL', value: 66, color: '#ffd77e' }, { label: 'NECKLACE', value: 41.42, color: '#d4e56b' },
  { label: 'PLATINUM', value: 6.9, color: '#8dbb5d' }, { label: 'RING', value: 321.453, color: '#63ad61' },
  { label: 'STUD', value: 23.72, color: '#26824e' }, { label: 'THALI', value: 12.9, color: '#12613f' }
];
export const stockRows = [
  ['BACK OFFICE', '33', '1,034.160', '1,051.720'], ['BANGLE', '1,770', '28,514.690', '29,422.400'],
  ['CHAIN', '627', '16,813.690', '17,146.940'], ['COIN', '192', '873.150', '873.150'],
  ['DIAMOND', '3,491', '28,344.740', '29,132.670']
];
export const dashboardKpis = [{ title: "Today's Purchase Count", value: '36' }, { title: "Today's OG Quote Count", value: '36' }, { title: "Today's Sales Quote Count", value: '84' }, { title: "Today's Sale Count", value: '84' }];
export const dashboardRings = [{ name: 'PUR - GOLD COIN', progress: 6.6, total: '200' }, { name: 'PUR - GOLD JEWEL', progress: 86, total: '2,611' }, { name: 'PUR - URD DIAMOND', progress: .1, total: '3' }, { name: 'PUR - WORNOUT', progress: 7.2, total: '217' }];
export const dashboardTabs = ['Available Widgets', 'Build Widget', 'Create Tab', 'Available Chart'];
export const dashboardTabRows = [{ name: 'MAIN', order: 1, default: 'Y' }, { name: 'Inventory', order: 2, default: 'N' }, { name: 'Sales', order: 3, default: 'N' }];
export const schemeFields = [
  ['Scheme Code', 'GOLD0001', 'required'], ['Scheme Name', 'AADI 11 MONTHS SCHEME', 'required'],
  ['Scheme Type', 'Weight', 'select'], ['Accounting Purity', '18K', 'select'], ['Item Category', 'GOLD', 'select'],
  ['Lucky Draw', 'toggle'], ['Fixed Installment', 'toggle'], ['No. of Installments', '11', 'number'],
  ['Min. Installment For Bonus', '1', 'number'], ['Common Member No. For All Groups', 'toggle'],
  ['Auto Card No./Acct No.', 'toggle'], ['Advance Saving Scheme', 'toggle'], ['Rate On', 'radio'],
  ['Discount Setup', 'Select Discount Setup', 'select'], ['Page Template', 'Terms and conditions', 'select'],
  ['Active', 'toggle'], ['Bonus Weight Flag', 'toggle'], ['Amount UnFix Flag', 'toggle']
];
export const chitFields = [
  ['Chit Group Name', 'AJS AADI 11 MONTHS SCHEME'], ['Chit Group Code', '11'], ['Scheme Name', 'AADI 11 MONTHS SCHEME'],
  ['Chit Type', 'Weight'], ['Chit Period', 'MONTHLY'], ['Bonus Ledger', 'Select Ledger'], ['Start Date', '01-04-2024'],
  ['Accounting Purity', '18K'], ['Weight Type', 'Value'], ['No. of installments', '11'], ['Cooling Period', ''],
  ['Installment Amount', '2,000.00'], ['Incentive Cut-off Date', '2'], ['Incentive %', '0.00'], ['Incentive Amount', ''],
  ['Maturity Amount', '22,000.00'], ['Lucky Draw', 'toggle'], ['Draw Amount', ''], ['Maximum Members', ''],
  ['No. Series', 'CHIT'], ['Employee Incentive', ''], ['Employee Incentive In ?', 'Select'],
  ['Redeem Rate On', 'radio'], ['Max Installment Amount', ''], ['Cap Amount', ''], ['First Installment Amount', '']
];
