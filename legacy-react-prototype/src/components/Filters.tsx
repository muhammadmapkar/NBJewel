import { CATEGORIES, STORES, type Category, type Store } from '../data/generateData'

type Props = { store: Store | 'All stores'; category: Category | 'All categories'; setStore: (v: Store | 'All stores') => void; setCategory: (v: Category | 'All categories') => void; date?: { from: string; to: string }; setDate?: (v: { from: string; to: string }) => void }
export function Filters({ store, category, setStore, setCategory, date, setDate }: Props) {
  return <div className="flex flex-wrap items-end gap-2.5">
    <label className="flex min-w-[142px] flex-1 flex-col gap-1 text-[10px] font-bold uppercase tracking-widest text-slate-500">Store
      <select value={store} onChange={(e) => setStore(e.target.value as Store | 'All stores')} className="h-9 rounded-lg border border-[#303846] bg-[#171d26] px-3 text-xs text-slate-200 outline-none focus:border-gold">{['All stores', ...STORES].map((x) => <option key={x}>{x}</option>)}</select>
    </label>
    <label className="flex min-w-[142px] flex-1 flex-col gap-1 text-[10px] font-bold uppercase tracking-widest text-slate-500">Category
      <select value={category} onChange={(e) => setCategory(e.target.value as Category | 'All categories')} className="h-9 rounded-lg border border-[#303846] bg-[#171d26] px-3 text-xs text-slate-200 outline-none focus:border-gold">{['All categories', ...CATEGORIES].map((x) => <option key={x}>{x}</option>)}</select>
    </label>
    {date && setDate && <><label className="flex min-w-[135px] flex-1 flex-col gap-1 text-[10px] font-bold uppercase tracking-widest text-slate-500">From<input type="date" value={date.from} onChange={(e) => setDate({ ...date, from: e.target.value })} className="h-9 rounded-lg border border-[#303846] bg-[#171d26] px-3 text-xs text-slate-200 outline-none focus:border-gold" /></label><label className="flex min-w-[135px] flex-1 flex-col gap-1 text-[10px] font-bold uppercase tracking-widest text-slate-500">To<input type="date" value={date.to} onChange={(e) => setDate({ ...date, to: e.target.value })} className="h-9 rounded-lg border border-[#303846] bg-[#171d26] px-3 text-xs text-slate-200 outline-none focus:border-gold" /></label></>}
  </div>
}
