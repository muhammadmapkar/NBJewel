import { useEffect, useMemo, useState } from 'react'
import {
  Bar,
  BarChart,
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import {
  Bot,
  ChevronDown,
  Download,
  Flame,
  Gem,
  PackageSearch,
  RefreshCw,
  Scale,
  Search,
  ShieldAlert,
  Sparkles,
  TrendingUp,
  Users,
} from 'lucide-react'
import { festivals } from './config/festivals'
import { CATEGORIES, STORES, type Category, type Store } from './data/generateData'
import { getForecast, getRfm, type Filters, type RfmRow } from './services/api'
import { Filters as FilterControls } from './components/Filters'
import { SampleBadge, StatusDot } from './components/Badge'
import { AssumptionsPanel } from './components/AssumptionsPanel'
import { GoldReconciliationPage } from './components/GoldReconciliationPage'
import { CompliancePage } from './components/CompliancePage'
import { MeltMarkdownHoldPage } from './components/MeltMarkdownHoldPage'
import { DesignSearchPage } from './components/DesignSearchPage'
import { AskDataPage } from './components/AskDataPage'

const gold = '#d6a84f', grid = '#29313e'
const fmt = new Intl.NumberFormat('en-IN', { maximumFractionDigits: 0 })
const money = (n: number) => `₹${fmt.format(n)}`
const actions: Record<string, string> = {
  Champions: 'Invite to priority preview',
  Loyal: 'Personalised cross-sell',
  'Big Spenders': 'Concierge relationship call',
  'At Risk': 'WhatsApp offer before next festival',
  New: 'Welcome to gold scheme',
  Lost: 'Win-back with rate lock',
}

function Card({ children, className = '' }: { children: React.ReactNode; className?: string }) {
  return <div className={`glass rounded-2xl p-4 sm:p-5 ${className}`}>{children}</div>
}

function Kpi({
  label,
  value,
  note,
  icon: Icon,
  tone = 'gold',
}: {
  label: string
  value: string
  note: string
  icon: typeof Users
  tone?: 'gold' | 'red' | 'mint'
}) {
  return (
    <Card>
      <div className="flex items-start justify-between">
        <span className="text-[11px] font-bold uppercase tracking-widest text-slate-500">{label}</span>
        <span
          className={`rounded-lg p-2 ${
            tone === 'red'
              ? 'bg-danger/10 text-danger'
              : tone === 'mint'
                ? 'bg-mint/10 text-mint'
                : 'bg-gold/10 text-gold'
          }`}
        >
          <Icon size={16} />
        </span>
      </div>
      <div className="mt-4 text-2xl font-extrabold tracking-tight text-slate-100">{value}</div>
      <div className="mt-1 text-xs text-slate-500">{note}</div>
    </Card>
  )
}

function ChartTip({ active, payload, label }: { active?: boolean; payload?: { value: number; name: string; color: string }[]; label?: string }) {
  return active && payload?.length ? (
    <div className="rounded-lg border border-[#343e4d] bg-[#11161e] p-3 text-xs shadow-xl">
      <div className="mb-1 text-slate-500">{label}</div>
      {payload.map((p) => (
        <div key={p.name} className="font-bold" style={{ color: p.color }}>
          {p.name}: {fmt.format(p.value)}
        </div>
      ))}
    </div>
  ) : null
}

function Loading() {
  return (
    <div className="grid gap-4 sm:grid-cols-3">
      {[1, 2, 3].map((x) => (
        <div key={x} className="skeleton h-28 rounded-2xl" />
      ))}
    </div>
  )
}

function RfmPage({ filters, setFilters }: { filters: Filters; setFilters: (v: Filters) => void }) {
  const [rows, setRows] = useState<RfmRow[]>([])
  const [loading, setLoading] = useState(true)
  const [segment, setSegment] = useState<string | null>(null)

  useEffect(() => {
    setLoading(true)
    getRfm(filters).then(setRows).finally(() => setLoading(false))
  }, [filters])

  const summary = useMemo(
    () =>
      [...new Set(rows.map((r) => r.segment))].map((name) => ({
        name,
        size: rows.filter((r) => r.segment === name).length,
        revenue: rows.filter((r) => r.segment === name).reduce((a, r) => a + r.monetary, 0),
        avgSpend:
          rows.filter((r) => r.segment === name).reduce((a, r) => a + r.monetary, 0) /
          Math.max(1, rows.filter((r) => r.segment === name).length),
        avgDays:
          rows.filter((r) => r.segment === name).reduce((a, r) => a + r.recency, 0) /
          Math.max(1, rows.filter((r) => r.segment === name).length),
      })),
    [rows]
  )

  const exportCsv = () => {
    const csv = [
      'Customer,Phone,Segment,Recency,Frequency,Monetary',
      ...rows.map((r) => `"${r.name}",${r.phone},${r.segment},${r.recency},${r.frequency},${r.monetary}`),
    ].join('\n')
    const a = document.createElement('a')
    a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }))
    a.download = 'nb-jewel-rfm.csv'
    a.click()
    URL.revokeObjectURL(a.href)
  }

  const atRiskRevenue = rows.filter((r) => r.segment === 'At Risk').reduce((a, r) => a + r.monetary, 0)

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <div className="mb-3">
            <SampleBadge />
          </div>
          <h1 className="text-2xl font-extrabold tracking-tight sm:text-3xl">
            Customer segments <span className="text-gold">(RFM)</span>
          </h1>
          <p className="mt-1 text-sm text-slate-500">Know who to call before the next celebration.</p>
        </div>
        <div className="flex flex-wrap items-end gap-2">
          <FilterControls
            store={filters.store}
            category={filters.category}
            setStore={(store) => setFilters({ ...filters, store })}
            setCategory={(category) => setFilters({ ...filters, category })}
            date={{ from: filters.from!, to: filters.to! }}
            setDate={(date) => setFilters({ ...filters, ...date })}
          />
          <button
            onClick={exportCsv}
            className="flex h-9 items-center gap-2 rounded-lg border border-gold/30 bg-gold/10 px-3 text-xs font-bold text-gold hover:bg-gold/20"
          >
            <Download size={14} /> Export CSV
          </button>
        </div>
      </div>

      {loading ? (
        <Loading />
      ) : (
        <>
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <Kpi label="Total customers" value={fmt.format(rows.length)} note="In the 24 month view" icon={Users} />
            <Kpi
              label="Champions"
              value={fmt.format(rows.filter((r) => r.segment === 'Champions').length)}
              note="Active, high-value customers"
              icon={Gem}
              tone="mint"
            />
            <Kpi
              label="At risk"
              value={fmt.format(rows.filter((r) => r.segment === 'At Risk').length)}
              note="Needs a timely nudge"
              icon={TrendingUp}
              tone="red"
            />
            <Kpi
              label="Revenue at risk"
              value={money(atRiskRevenue)}
              note="Historical spend at risk"
              icon={PackageSearch}
            />
          </div>

          <div className="grid gap-4 lg:grid-cols-2">
            <Card>
              <div className="mb-4">
                <h2 className="font-bold">Customers by segment</h2>
                <p className="text-xs text-slate-500">Click a bar or table row to explore</p>
              </div>
              <ResponsiveContainer width="100%" height={230}>
                <BarChart
                  data={summary}
                  layout="vertical"
                  margin={{ left: 8, right: 20 }}
                  onClick={(e) => e?.activeLabel && setSegment(String(e.activeLabel))}
                >
                  <CartesianGrid stroke={grid} horizontal={false} />
                  <XAxis type="number" stroke="#687386" fontSize={11} />
                  <YAxis type="category" dataKey="name" stroke="#9aa4b4" fontSize={11} width={90} />
                  <Tooltip content={<ChartTip />} />
                  <Bar dataKey="size" name="Customers" fill={gold} radius={[0, 5, 5, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </Card>

            <Card>
              <div className="mb-4">
                <h2 className="font-bold">Revenue by segment</h2>
                <p className="text-xs text-slate-500">Total historical value in the selected window</p>
              </div>
              <ResponsiveContainer width="100%" height={230}>
                <BarChart data={summary} margin={{ left: 0, right: 10 }}>
                  <CartesianGrid stroke={grid} vertical={false} />
                  <XAxis dataKey="name" stroke="#9aa4b4" fontSize={10} tickLine={false} />
                  <YAxis stroke="#687386" fontSize={10} tickFormatter={(v) => `₹${Math.round(v / 1000)}k`} />
                  <Tooltip content={<ChartTip />} />
                  <Bar dataKey="revenue" name="Revenue" fill="#6dd5b0" radius={[5, 5, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </Card>
          </div>

          <Card>
            <div className="mb-4 flex items-center justify-between">
              <div>
                <h2 className="font-bold">Segment playbook</h2>
                <p className="text-xs text-slate-500">A simple next best action for every group</p>
              </div>
              <span className="mono text-[10px] text-slate-600">RFM / 24 MONTHS</span>
            </div>
            <div className="scrollbar overflow-x-auto">
              <table className="w-full min-w-[720px] text-left text-xs">
                <thead className="border-b border-[#29313e] text-[10px] uppercase tracking-widest text-slate-500">
                  <tr>
                    <th className="pb-3">Segment</th>
                    <th className="pb-3">Size</th>
                    <th className="pb-3">Avg spend</th>
                    <th className="pb-3">Avg days since purchase</th>
                    <th className="pb-3">Recommended action</th>
                  </tr>
                </thead>
                <tbody>
                  {summary.map((row) => (
                    <tr
                      key={row.name}
                      onClick={() => setSegment(row.name)}
                      className={`cursor-pointer border-b border-[#202733] transition hover:bg-white/[.03] ${
                        segment === row.name ? 'bg-gold/[.05]' : ''
                      }`}
                    >
                      <td className="py-3 font-bold text-slate-200">{row.name}</td>
                      <td className="py-3 text-slate-400">{fmt.format(row.size)}</td>
                      <td className="py-3 text-slate-300">{money(row.avgSpend)}</td>
                      <td className="py-3 text-slate-400">{Math.round(row.avgDays)} days</td>
                      <td className="py-3 text-gold">{actions[row.name]}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>

          <AssumptionsPanel
            title="Customer Segmentation (RFM) Assumptions"
            assumptions={[
              'Analysis Window: Evaluates sales over the rolling 24 month transaction history.',
              'Scoring Model: Quintile binning (1 to 5 scale) on Recency, Frequency, and Monetary spend.',
              'Scheme Bonus: Active gold savings scheme enrolment adds a +1 score boost.',
              'At Risk Classification: Historical spend above threshold with recency between 180 and 730 days.',
            ]}
          />
        </>
      )}
    </div>
  )
}

function ForecastPage({ filters, setFilters }: { filters: Filters; setFilters: (v: Filters) => void }) {
  const [festival, setFestival] = useState('Dhanteras')
  const [data, setData] = useState<Awaited<ReturnType<typeof getForecast>> | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    setLoading(true)
    getForecast(filters, festival).then(setData).finally(() => setLoading(false))
  }, [filters, festival])

  const exportCsv = () => {
    if (!data) return
    const headers = ['Category,Expected Grams,Pieces,Expected Value (INR),Current Stock (Grams),Reorder (Grams),Risk']
    const rows = data.categories.map((c) =>
      [
        `"${c.category}"`,
        c.grams,
        c.pieces,
        c.value,
        c.stock,
        Math.max(0, c.grams - c.stock),
        c.risk,
      ].join(',')
    )
    const csv = [headers.join(','), ...rows].join('\n')
    const blob = new Blob([csv], { type: 'text/csv' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `festival-forecast-${festival.replace(/\s+/g, '-').toLowerCase()}.csv`
    a.click()
    URL.revokeObjectURL(url)
  }

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <div className="mb-3">
            <SampleBadge />
          </div>
          <h1 className="text-2xl font-extrabold tracking-tight sm:text-3xl">
            Festival demand <span className="text-gold">forecast</span>
          </h1>
          <p className="mt-1 text-sm text-slate-500">Plan stock with a seasonal signal, not a gut feeling.</p>
        </div>
        <div className="flex flex-wrap items-end gap-2">
          <FilterControls
            store={filters.store}
            category={filters.category}
            setStore={(store) => setFilters({ ...filters, store })}
            setCategory={(category) => setFilters({ ...filters, category })}
          />
          <label className="flex min-w-[170px] flex-1 flex-col gap-1 text-[10px] font-bold uppercase tracking-widest text-slate-500">
            Festival
            <select
              value={festival}
              onChange={(e) => setFestival(e.target.value)}
              className="h-9 rounded-lg border border-[#303846] bg-[#171d26] px-3 text-xs text-slate-200 outline-none focus:border-gold"
            >
              {[...new Set(festivals.map((f) => f.name))].map((x) => (
                <option key={x}>{x}</option>
              ))}
            </select>
          </label>
          <button
            onClick={exportCsv}
            className="flex h-9 items-center gap-2 rounded-lg border border-gold/30 bg-gold/10 px-3 text-xs font-bold text-gold hover:bg-gold/20"
          >
            <Download size={14} /> Export CSV
          </button>
        </div>
      </div>

      {loading || !data ? (
        <Loading />
      ) : (
        <>
          <div className="grid gap-4 lg:grid-cols-[1fr_280px]">
            <Card>
              <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
                <div>
                  <h2 className="font-bold">{festival} window</h2>
                  <p className="text-xs text-slate-500">
                    28 days before and after : Festival date{' '}
                    {new Date(data.festivalDate).toLocaleDateString('en-IN', {
                      day: 'numeric',
                      month: 'short',
                      year: 'numeric',
                    })}
                  </p>
                </div>
                <div className="flex items-center gap-3 text-[10px] text-slate-500">
                  <span>
                    <i className="mr-1 inline-block h-2 w-2 rounded-full bg-mint" />
                    Actual
                  </span>
                  <span>
                    <i className="mr-1 inline-block h-2 w-2 rounded-full bg-gold" />
                    Forecast
                  </span>
                </div>
              </div>
              <ResponsiveContainer width="100%" height={280}>
                <LineChart data={data.series} margin={{ left: 0, right: 10 }}>
                  <CartesianGrid stroke={grid} vertical={false} />
                  <XAxis
                    dataKey="date"
                    stroke="#687386"
                    fontSize={10}
                    tickFormatter={(v) => v.slice(5)}
                    interval={6}
                  />
                  <YAxis stroke="#687386" fontSize={10} />
                  <Tooltip content={<ChartTip />} />
                  <Line dataKey="actual" name="Actual grams" stroke="#6dd5b0" strokeWidth={2} dot={false} connectNulls />
                  <Line
                    dataKey="forecast"
                    name="Forecast grams"
                    stroke={gold}
                    strokeWidth={2}
                    strokeDasharray="5 5"
                    dot={false}
                  />
                </LineChart>
              </ResponsiveContainer>
            </Card>

            <Card className="flex flex-col justify-between">
              <div>
                <div className="flex items-center justify-between">
                  <h2 className="font-bold">Backtest honesty</h2>
                  <RefreshCw size={15} className="text-gold" />
                </div>
                <p className="mt-2 text-xs leading-relaxed text-slate-500">
                  We hid last year's festival and ran this same simple model against it.
                </p>
              </div>
              <div className="my-7">
                <div className="text-5xl font-extrabold text-gold">{data.wape}%</div>
                <div className="mt-1 text-xs font-bold uppercase tracking-widest text-slate-500">WAPE error</div>
              </div>
              <div className="rounded-lg border border-[#29313e] bg-[#10151c] p-3 text-xs text-slate-400">
                Model: <span className="text-slate-200">last year x 1.06 growth</span>
                <br />
                Gold price adjustment included
              </div>
            </Card>
          </div>

          <Card>
            <div className="mb-4 flex items-center justify-between">
              <div>
                <h2 className="font-bold">Category reorder plan</h2>
                <p className="text-xs text-slate-500">Forecast minus current stock : simulated inventory</p>
              </div>
              <span className="mono text-[10px] text-slate-600">UNITS: GRAMS / PIECES</span>
            </div>
            <div className="scrollbar overflow-x-auto">
              <table className="w-full min-w-[690px] text-left text-xs">
                <thead className="border-b border-[#29313e] text-[10px] uppercase tracking-widest text-slate-500">
                  <tr>
                    <th className="pb-3">Category</th>
                    <th className="pb-3">Expected grams</th>
                    <th className="pb-3">Pieces</th>
                    <th className="pb-3">Expected value</th>
                    <th className="pb-3">Current stock</th>
                    <th className="pb-3">Reorder</th>
                    <th className="pb-3">Risk</th>
                  </tr>
                </thead>
                <tbody>
                  {data.categories.map((r) => (
                    <tr key={r.category} className="border-b border-[#202733]">
                      <td className="py-3 font-bold text-slate-200">{r.category}</td>
                      <td className="py-3 text-slate-300">{fmt.format(r.grams)}g</td>
                      <td className="py-3 text-slate-400">{fmt.format(r.pieces)}</td>
                      <td className="py-3 text-slate-300">{money(r.value)}</td>
                      <td className="py-3 text-slate-400">{fmt.format(r.stock)}g</td>
                      <td className="py-3 font-bold text-gold">{fmt.format(Math.max(0, r.grams - r.stock))}g</td>
                      <td className="py-3">
                        <StatusDot status={r.risk} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>

          <AssumptionsPanel
            title="Festival Demand Forecasting Assumptions"
            assumptions={[
              'Seasonal Baseline: Sales window centered +/- 28 days around the festival date in 2024 and 2025.',
              'Growth Assumption: Assumes a 6% organic year-over-year volume growth rate.',
              'Gold Price Elasticity: Forecast is adjusted downwards when current gold rates significantly exceed prior year rates.',
              'Reorder Logic: Reorder recommendation = Max(0, Expected Grams minus Current Stock).',
            ]}
          />
        </>
      )}
    </div>
  )
}

export type TabId =
  | 'rfm'
  | 'forecast'
  | 'gold-reconciliation'
  | 'melt-markdown-hold'
  | 'design-search'
  | 'compliance'
  | 'assistant'

interface NavGroup {
  groupName: string
  tabs: { id: TabId; label: string; icon: any }[]
}

const NAV_GROUPS: NavGroup[] = [
  {
    groupName: 'Customers',
    tabs: [
      { id: 'rfm', label: 'Segments (RFM)', icon: Users },
      { id: 'forecast', label: 'Forecast', icon: TrendingUp },
    ],
  },
  {
    groupName: 'Stock',
    tabs: [
      { id: 'gold-reconciliation', label: 'Gold Reconciliation', icon: Scale },
      { id: 'melt-markdown-hold', label: 'Melt-Markdown-Hold', icon: Flame },
      { id: 'design-search', label: 'Design Search', icon: Search },
    ],
  },
  {
    groupName: 'Compliance',
    tabs: [{ id: 'compliance', label: 'Exception Engine', icon: ShieldAlert }],
  },
  {
    groupName: 'AI',
    tabs: [{ id: 'assistant', label: 'Ask Your Data', icon: Bot }],
  },
]

export default function App() {
  const [tab, setTab] = useState<TabId>('rfm')
  const [filters, setFilters] = useState<Filters>({
    store: 'All stores',
    category: 'All categories',
    from: '2024-09-28',
    to: '2026-09-28',
  })

  return (
    <div className="min-h-screen bg-[#090c10] text-slate-100">
      {/* Top Header */}
      <header className="border-b border-[#202733] bg-[#0c0f14]/80 backdrop-blur-xl sticky top-0 z-30">
        <div className="mx-auto flex max-w-[1400px] items-center justify-between px-4 py-3.5 sm:px-6 lg:px-8">
          <div className="flex items-center gap-3">
            <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-gold text-[#17120b] shadow-md shadow-gold/20">
              <Gem size={20} />
            </div>
            <div>
              <div className="text-sm font-extrabold tracking-wide text-slate-100">NB Jewel Insights</div>
              <div className="text-[10px] text-slate-500">Enterprise Jewelry Intelligence (Prototype)</div>
            </div>
          </div>
          <SampleBadge />
        </div>
      </header>

      {/* Main Content Container */}
      <main className="mx-auto max-w-[1400px] px-4 py-6 sm:px-6 sm:py-8 lg:px-8">
        {/* Navigation Grouping Bar */}
        <div className="mb-7">
          {/* Mobile Tab Selector (Dropdown) */}
          <div className="sm:hidden mb-4">
            <label className="text-[10px] font-bold uppercase tracking-wider text-slate-500 block mb-1">
              Navigate Module
            </label>
            <div className="relative">
              <select
                value={tab}
                onChange={(e) => setTab(e.target.value as TabId)}
                className="h-10 w-full appearance-none rounded-xl border border-[#2b3342] bg-[#121722] px-3.5 text-xs font-bold text-slate-200 outline-none focus:border-gold"
              >
                {NAV_GROUPS.map((group) => (
                  <optgroup key={group.groupName} label={group.groupName}>
                    {group.tabs.map((t) => (
                      <option key={t.id} value={t.id}>
                        {group.groupName}: {t.label}
                      </option>
                    ))}
                  </optgroup>
                ))}
              </select>
              <ChevronDown size={15} className="absolute right-3 top-3 text-slate-400 pointer-events-none" />
            </div>
          </div>

          {/* Desktop Grouped Tabs */}
          <nav className="hidden sm:flex flex-wrap items-center gap-2 rounded-2xl border border-[#252c38] bg-[#11161e] p-1.5">
            {NAV_GROUPS.map((group, gIdx) => (
              <div key={group.groupName} className="flex items-center">
                {gIdx > 0 && <div className="h-5 w-[1px] bg-[#222a36] mx-1.5" />}
                <span className="px-2 text-[10px] font-black uppercase tracking-wider text-slate-500">
                  {group.groupName}
                </span>
                <div className="flex gap-1">
                  {group.tabs.map(({ id, label, icon: Icon }) => {
                    const isActive = tab === id
                    return (
                      <button
                        key={id}
                        onClick={() => setTab(id)}
                        className={`flex items-center gap-1.5 rounded-xl px-3 py-1.5 text-xs font-bold transition ${
                          isActive
                            ? 'bg-gold text-[#17120b] shadow-sm'
                            : 'text-slate-400 hover:text-slate-100 hover:bg-white/[.04]'
                        }`}
                      >
                        <Icon size={14} />
                        {label}
                      </button>
                    )
                  })}
                </div>
              </div>
            ))}
          </nav>
        </div>

        {/* Tab View Routing */}
        {tab === 'rfm' && <RfmPage filters={filters} setFilters={setFilters} />}
        {tab === 'forecast' && <ForecastPage filters={filters} setFilters={setFilters} />}
        {tab === 'gold-reconciliation' && <GoldReconciliationPage />}
        {tab === 'compliance' && <CompliancePage />}
        {tab === 'melt-markdown-hold' && <MeltMarkdownHoldPage />}
        {tab === 'design-search' && <DesignSearchPage />}
        {tab === 'assistant' && <AskDataPage />}
      </main>

      {/* Footer */}
      <footer className="mx-auto max-w-[1400px] border-t border-[#202733] px-4 py-5 text-center text-[11px] text-slate-600 sm:px-6 lg:px-8">
        Prototype on deterministic sample data. Connects to enterprise ERP via SQL view, nightly summary table, and .NET API.
      </footer>
    </div>
  )
}
