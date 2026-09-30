import { useEffect, useState } from 'react'
import {
  AlertOctagon,
  AlertTriangle,
  CheckCircle2,
  Download,
  FileSpreadsheet,
  FileText,
  Filter,
  Info,
  Printer,
  ShieldAlert,
  Sliders,
  X,
} from 'lucide-react'
import {
  getComplianceExceptions,
  type ComplianceFilters,
  type ComplianceScanResult,
} from '../services/api'
import { defaultComplianceConfig, type ComplianceConfig } from '../config/complianceConfig'
import { allRules, type Severity } from '../rules/complianceRules'
import { STORES, type Store } from '../data/generateData'
import { SampleBadge } from './Badge'
import { AssumptionsPanel } from './AssumptionsPanel'

const fmt = new Intl.NumberFormat('en-IN', { maximumFractionDigits: 0 })

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
  icon: typeof ShieldAlert
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

function Loading() {
  return (
    <div className="space-y-4">
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {[1, 2, 3, 4].map((x) => (
          <div key={x} className="skeleton h-28 rounded-2xl" />
        ))}
      </div>
      <div className="skeleton h-80 rounded-2xl" />
    </div>
  )
}

export function CompliancePage() {
  const [filters, setFilters] = useState<ComplianceFilters>({
    country: 'All countries',
    ruleId: 'All rules',
    severity: 'All severities',
    store: 'All stores',
  })

  const [config, setConfig] = useState<ComplianceConfig>(defaultComplianceConfig)
  const [data, setData] = useState<ComplianceScanResult | null>(null)
  const [loading, setLoading] = useState(true)
  const [showPrintPack, setShowPrintPack] = useState(false)
  const [showConfigModal, setShowConfigModal] = useState(false)

  useEffect(() => {
    setLoading(true)
    getComplianceExceptions(filters, config)
      .then(setData)
      .finally(() => setLoading(false))
  }, [filters, config])

  const exportCsv = () => {
    if (!data) return
    const headers = [
      'Invoice ID',
      'Date',
      'Store',
      'Country',
      'Rule ID',
      'Rule Name',
      'Severity',
      'Reason',
      'Suggested Fix',
      'Value at Risk',
      'Currency',
    ]
    const rows = data.exceptions.map((e) =>
      [
        e.invoiceId,
        e.date,
        `"${e.store}"`,
        e.country,
        e.ruleId,
        `"${e.ruleName}"`,
        e.severity,
        `"${e.reason.replace(/"/g, '""')}"`,
        `"${e.suggestedFix.replace(/"/g, '""')}"`,
        e.valueAtRisk,
        e.currency,
      ].join(',')
    )
    const csv = [headers.join(','), ...rows].join('\n')
    const blob = new Blob([csv], { type: 'text/csv' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `compliance-exceptions-${filters.country.toLowerCase()}.csv`
    a.click()
    URL.revokeObjectURL(url)
  }

  const handlePrint = () => {
    window.print()
  }

  return (
    <div className="space-y-5">
      {/* Statutory Advisory Banner */}
      <div className="flex items-center gap-3 rounded-xl border border-amber-500/30 bg-amber-500/10 px-4 py-3 text-xs text-amber-200">
        <AlertTriangle size={18} className="shrink-0 text-amber-400" />
        <div className="flex-1 font-medium">
          Flags for review. Not tax advice. Verify all rules with current official notices.
        </div>
        <button
          onClick={() => setShowConfigModal(true)}
          className="flex items-center gap-1.5 rounded-lg border border-amber-500/30 bg-amber-500/20 px-2.5 py-1 text-[11px] font-bold text-amber-300 hover:bg-amber-500/30"
        >
          <Sliders size={13} /> Rule Toggles
        </button>
      </div>

      {/* Header */}
      <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <div className="mb-3">
            <SampleBadge />
          </div>
          <h1 className="text-2xl font-extrabold tracking-tight sm:text-3xl">
            Compliance <span className="text-gold">Exception Engine</span>
          </h1>
          <p className="mt-1 text-sm text-slate-500">
            Cross-border statutory surveillance for UAE FTA VAT / E-Invoicing and Indian BIS HUID / Cash caps.
          </p>
        </div>

        {/* Global Controls & Filters */}
        <div className="flex flex-wrap items-end gap-2.5">
          <label className="flex min-w-[120px] flex-1 flex-col gap-1 text-[10px] font-bold uppercase tracking-widest text-slate-500">
            Country
            <select
              value={filters.country}
              onChange={(e) => setFilters({ ...filters, country: e.target.value as any })}
              className="h-9 rounded-lg border border-[#303846] bg-[#171d26] px-3 text-xs text-slate-200 outline-none focus:border-gold"
            >
              <option value="All countries">All countries</option>
              <option value="UAE">UAE</option>
              <option value="India">India</option>
            </select>
          </label>

          <label className="flex min-w-[130px] flex-1 flex-col gap-1 text-[10px] font-bold uppercase tracking-widest text-slate-500">
            Store
            <select
              value={filters.store}
              onChange={(e) => setFilters({ ...filters, store: e.target.value as Store | 'All stores' })}
              className="h-9 rounded-lg border border-[#303846] bg-[#171d26] px-3 text-xs text-slate-200 outline-none focus:border-gold"
            >
              {['All stores', ...STORES].map((s) => (
                <option key={s}>{s}</option>
              ))}
            </select>
          </label>

          <label className="flex min-w-[140px] flex-1 flex-col gap-1 text-[10px] font-bold uppercase tracking-widest text-slate-500">
            Rule
            <select
              value={filters.ruleId}
              onChange={(e) => setFilters({ ...filters, ruleId: e.target.value })}
              className="h-9 rounded-lg border border-[#303846] bg-[#171d26] px-3 text-xs text-slate-200 outline-none focus:border-gold"
            >
              <option value="All rules">All rules</option>
              {allRules.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.id}: {r.name}
                </option>
              ))}
            </select>
          </label>

          <label className="flex min-w-[110px] flex-1 flex-col gap-1 text-[10px] font-bold uppercase tracking-widest text-slate-500">
            Severity
            <select
              value={filters.severity}
              onChange={(e) => setFilters({ ...filters, severity: e.target.value as any })}
              className="h-9 rounded-lg border border-[#303846] bg-[#171d26] px-3 text-xs text-slate-200 outline-none focus:border-gold"
            >
              <option value="All severities">All severities</option>
              <option value="High">High</option>
              <option value="Medium">Medium</option>
              <option value="Low">Low</option>
            </select>
          </label>

          <button
            onClick={() => setShowPrintPack(!showPrintPack)}
            className={`flex h-9 items-center gap-2 rounded-lg border px-3 text-xs font-bold transition ${
              showPrintPack
                ? 'border-gold bg-gold text-slate-900'
                : 'border-[#303846] bg-[#171d26] text-slate-300 hover:text-white'
            }`}
          >
            <FileSpreadsheet size={14} /> Monthly Pack
          </button>

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
          {/* KPI Cards */}
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <Kpi
              label="Invoices Scanned"
              value={fmt.format(data.kpis.scannedCount)}
              note="Across 3 regional locations"
              icon={FileText}
              tone="mint"
            />
            <Kpi
              label="Exceptions Found"
              value={fmt.format(data.kpis.exceptionsCount)}
              note={`${((data.kpis.exceptionsCount / data.kpis.scannedCount) * 100).toFixed(1)}% exception rate`}
              icon={AlertOctagon}
              tone={data.kpis.exceptionsCount > 0 ? 'red' : 'mint'}
            />
            <Kpi
              label="High Severity Count"
              value={fmt.format(data.kpis.highSeverityCount)}
              note="Direct statutory penalty risk"
              icon={ShieldAlert}
              tone="red"
            />
            <Kpi
              label="Est. Value at Risk"
              value={`AED ${fmt.format(data.kpis.estimatedValueAtRiskAED)}`}
              note="Consolidated regulatory risk exposure"
              icon={AlertTriangle}
              tone="gold"
            />
          </div>

          {/* Monthly Exception Pack View (Printable) */}
          {showPrintPack ? (
            <Card className="print:m-0 print:border-none print:p-0 print:bg-white print:text-black">
              <div className="mb-4 flex items-center justify-between border-b border-[#29313e] pb-4 print:border-b-2 print:border-black">
                <div>
                  <h2 className="text-lg font-bold text-slate-100 print:text-black">
                    Monthly Compliance Audit Pack
                  </h2>
                  <p className="text-xs text-slate-500 print:text-gray-600">
                    Audit documentation grouped by rule. Ready for legal/tax review before filing.
                  </p>
                </div>
                <div className="flex items-center gap-2 print:hidden">
                  <button
                    onClick={handlePrint}
                    className="flex items-center gap-1.5 rounded-lg border border-gold/30 bg-gold/15 px-3 py-1.5 text-xs font-bold text-gold hover:bg-gold/25"
                  >
                    <Printer size={14} /> Print Report
                  </button>
                  <button
                    onClick={() => setShowPrintPack(false)}
                    className="rounded-lg border border-[#303846] bg-[#171d26] px-3 py-1.5 text-xs font-bold text-slate-300 hover:text-white"
                  >
                    Back to Table
                  </button>
                </div>
              </div>

              {/* Grouped by Rule */}
              <div className="space-y-6">
                {data.summaryByRule.map((rule) => {
                  const ruleExceptions = data.exceptions.filter((e) => e.ruleId === rule.ruleId)
                  if (ruleExceptions.length === 0) return null

                  return (
                    <div
                      key={rule.ruleId}
                      className="rounded-xl border border-[#262f3d] bg-[#10151e] p-4 print:border-gray-300 print:bg-transparent"
                    >
                      <div className="flex items-center justify-between pb-2 border-b border-[#1f2632] mb-3">
                        <div className="flex items-center gap-2">
                          <span className="font-mono text-xs font-bold text-gold">{rule.ruleId}</span>
                          <span className="font-bold text-slate-200 print:text-black">{rule.ruleName}</span>
                          <span className="rounded bg-slate-800 px-2 py-0.5 text-[10px] text-slate-400 print:bg-gray-100 print:text-black">
                            {rule.country}
                          </span>
                        </div>
                        <span
                          className={`rounded px-2 py-0.5 text-[10px] font-bold ${
                            rule.severity === 'High'
                              ? 'bg-danger/20 text-danger'
                              : rule.severity === 'Medium'
                                ? 'bg-gold/20 text-gold'
                                : 'bg-slate-700 text-slate-300'
                          }`}
                        >
                          {rule.severity} Severity ({ruleExceptions.length} flags)
                        </span>
                      </div>

                      <div className="scrollbar overflow-x-auto">
                        <table className="w-full text-left text-xs">
                          <thead className="text-[10px] uppercase text-slate-500">
                            <tr>
                              <th className="pb-2">Invoice</th>
                              <th className="pb-2">Date</th>
                              <th className="pb-2">Store</th>
                              <th className="pb-2">Reason</th>
                              <th className="pb-2">Suggested Action</th>
                              <th className="pb-2 text-right">Exposure</th>
                            </tr>
                          </thead>
                          <tbody className="divide-y divide-[#1e2531]">
                            {ruleExceptions.slice(0, 10).map((exc, i) => (
                              <tr key={i} className="text-slate-300 print:text-black">
                                <td className="py-2 font-mono font-medium">{exc.invoiceId}</td>
                                <td className="py-2 text-slate-400">{exc.date}</td>
                                <td className="py-2">{exc.store}</td>
                                <td className="py-2 text-slate-300 max-w-[280px]">{exc.reason}</td>
                                <td className="py-2 text-gold print:text-black max-w-[280px]">
                                  {exc.suggestedFix}
                                </td>
                                <td className="py-2 text-right font-mono font-bold text-danger">
                                  {exc.currency} {fmt.format(exc.valueAtRisk)}
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    </div>
                  )
                })}
              </div>
            </Card>
          ) : (
            /* Exceptions Main Table */
            <Card>
              <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
                <div>
                  <h2 className="font-bold text-slate-100">Statutory Exception Register</h2>
                  <p className="text-xs text-slate-500">
                    Real-time automated compliance flags across invoices. Filter by rule or store to investigate.
                  </p>
                </div>
                <span className="font-mono text-[10px] text-slate-500">
                  {data.exceptions.length} EXCEPTIONS LISTED
                </span>
              </div>

              <div className="scrollbar max-h-96 overflow-y-auto overflow-x-auto">
                <table className="w-full min-w-[950px] text-left text-xs">
                  <thead className="sticky top-0 z-10 border-b border-[#29313e] bg-[#141a24] text-[10px] uppercase tracking-widest text-slate-400">
                    <tr>
                      <th className="py-2.5 pl-3">Invoice</th>
                      <th className="py-2.5">Date</th>
                      <th className="py-2.5">Store</th>
                      <th className="py-2.5">Rule</th>
                      <th className="py-2.5">Severity</th>
                      <th className="py-2.5">Violation Reason</th>
                      <th className="py-2.5">Suggested Action</th>
                      <th className="py-2.5 text-right pr-3">Exposure</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[#202733]">
                    {data.exceptions.map((exc, idx) => (
                      <tr key={idx} className="transition hover:bg-white/[.04]">
                        <td className="py-2.5 pl-3 font-mono font-medium text-slate-300">
                          {exc.invoiceId}
                        </td>
                        <td className="py-2.5 font-mono text-slate-400">{exc.date}</td>
                        <td className="py-2.5 text-slate-300">{exc.store}</td>
                        <td className="py-2.5 font-medium text-slate-200">
                          <span className="font-mono font-bold text-gold mr-1">{exc.ruleId}:</span>
                          {exc.ruleName}
                        </td>
                        <td className="py-2.5">
                          <span
                            className={`rounded-full px-2 py-0.5 text-[10px] font-bold ${
                              exc.severity === 'High'
                                ? 'bg-danger/15 text-danger border border-danger/30'
                                : exc.severity === 'Medium'
                                  ? 'bg-gold/15 text-gold border border-gold/30'
                                  : 'bg-slate-700/40 text-slate-300 border border-slate-600'
                            }`}
                          >
                            {exc.severity}
                          </span>
                        </td>
                        <td className="py-2.5 text-slate-300 max-w-[260px] leading-relaxed">
                          {exc.reason}
                        </td>
                        <td className="py-2.5 text-gold max-w-[280px] leading-relaxed">
                          {exc.suggestedFix}
                        </td>
                        <td className="py-2.5 text-right font-mono font-bold text-danger pr-3">
                          {exc.currency} {fmt.format(exc.valueAtRisk)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </Card>
          )}

          {/* Config / Toggles Modal */}
          {showConfigModal && (
            <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
              <div className="w-full max-w-lg rounded-2xl border border-[#303a4b] bg-[#121721] p-6 shadow-2xl">
                <div className="flex items-center justify-between pb-3 border-b border-[#232a37]">
                  <div className="flex items-center gap-2">
                    <Sliders size={18} className="text-gold" />
                    <h3 className="font-bold text-slate-100">Rule Thresholds & Toggles</h3>
                  </div>
                  <button
                    onClick={() => setShowConfigModal(false)}
                    className="rounded p-1 text-slate-400 hover:text-slate-100"
                  >
                    <X size={16} />
                  </button>
                </div>

                <div className="mt-4 space-y-4 text-xs text-slate-300">
                  {/* Silver toggle */}
                  <div className="rounded-xl border border-[#242c3a] bg-[#161c28] p-3.5">
                    <div className="flex items-center justify-between">
                      <div>
                        <div className="font-bold text-slate-200">Rule I6: Silver Hallmark Check</div>
                        <div className="text-[11px] text-slate-400 mt-0.5">
                          OFF by default. Official guidance was described differently across sources.
                        </div>
                      </div>
                      <label className="relative inline-flex cursor-pointer items-center">
                        <input
                          type="checkbox"
                          checked={config.silverHallmarkCheckEnabled}
                          onChange={(e) =>
                            setConfig({ ...config, silverHallmarkCheckEnabled: e.target.checked })
                          }
                          className="peer sr-only"
                        />
                        <div className="peer h-6 w-11 rounded-full bg-slate-700 after:absolute after:left-[2px] after:top-[2px] after:h-5 after:w-5 after:rounded-full after:bg-white after:transition-all after:content-[''] peer-checked:bg-gold peer-checked:after:translate-x-full" />
                      </label>
                    </div>
                    <div className="mt-2 text-[10px] text-gold/80 italic">
                      Note: {config.silverHallmarkGuidanceNote}
                    </div>
                  </div>

                  {/* India Section 269ST threshold */}
                  <div className="rounded-xl border border-[#242c3a] bg-[#161c28] p-3.5">
                    <div className="flex items-center justify-between">
                      <div>
                        <div className="font-bold text-slate-200">Section 269ST Cash Cap</div>
                        <div className="text-[11px] text-slate-400">Statutory threshold for cash receipt</div>
                      </div>
                      <span className="font-mono font-bold text-gold">₹{fmt.format(config.indiaSection269StCapInr)}</span>
                    </div>
                  </div>

                  {/* E-Invoicing toggle */}
                  <div className="rounded-xl border border-[#242c3a] bg-[#161c28] p-3.5 flex items-center justify-between">
                    <div>
                      <div className="font-bold text-slate-200">Rule U5: UAE E-Invoicing Schema Check</div>
                      <div className="text-[11px] text-slate-400">Enforce UUID and QR metadata validation</div>
                    </div>
                    <label className="relative inline-flex cursor-pointer items-center">
                      <input
                        type="checkbox"
                        checked={config.uaeEInvoicingEnforced}
                        onChange={(e) =>
                          setConfig({ ...config, uaeEInvoicingEnforced: e.target.checked })
                        }
                        className="peer sr-only"
                      />
                      <div className="peer h-6 w-11 rounded-full bg-slate-700 after:absolute after:left-[2px] after:top-[2px] after:h-5 after:w-5 after:rounded-full after:bg-white after:transition-all after:content-[''] peer-checked:bg-gold peer-checked:after:translate-x-full" />
                    </label>
                  </div>
                </div>

                <div className="mt-6 flex justify-end">
                  <button
                    onClick={() => setShowConfigModal(false)}
                    className="rounded-lg bg-gold px-4 py-2 text-xs font-bold text-slate-900 hover:bg-gold/90"
                  >
                    Save & Close
                  </button>
                </div>
              </div>
            </div>
          )}

          {/* Assumptions Collapsible Panel */}
          <AssumptionsPanel
            title="Compliance Rules and Threshold Assumptions"
            assumptions={[
              'UAE VAT Standard Rate: Set to 5% per Federal Decree-Law No. (8) of 2017 on Value Added Tax, applicable to both gold metal and craftsmanship making charges unless statutory reverse charge applies.',
              'B2B Reverse Charge Verification: Under Cabinet Decision No. (25) of 2018, reverse charge mechanism requires recipient to provide written declaration of tax registration and intent to resell or manufacture.',
              'India BIS Hallmarking: Mandates 6-digit alphanumeric HUID on gold jewellery. Silver hallmarking (Rule I6) remains optional/voluntary across several jurisdictions and is OFF by default.',
              'Cash Limits: Flags any Indian showroom cash receipt reaching or exceeding ₹200,000 to prevent 100% penalties under Section 271DA / Section 269ST.',
            ]}
            note="Verify against current FTA, BIS and tax notices before real use. Thresholds, tax exemptions, and e-invoicing rollout timelines vary across enterprise size."
          />
        </>
      )}
    </div>
  )
}
