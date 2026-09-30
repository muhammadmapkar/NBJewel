import { useState } from 'react'
import { ChevronDown, ChevronUp, HelpCircle } from 'lucide-react'

interface AssumptionsPanelProps {
  title?: string
  assumptions: string[]
  note?: string
}

export function AssumptionsPanel({
  title = 'Model Assumptions and Methodology',
  assumptions,
  note,
}: AssumptionsPanelProps) {
  const [open, setOpen] = useState(false)

  return (
    <div className="rounded-xl border border-[#2a3240] bg-[#121721] transition">
      <button
        onClick={() => setOpen(!open)}
        className="flex w-full items-center justify-between px-4 py-3 text-left text-xs font-bold text-slate-300 hover:text-gold"
      >
        <div className="flex items-center gap-2">
          <HelpCircle size={15} className="text-gold" />
          <span>{title}</span>
          <span className="rounded bg-gold/10 px-1.5 py-0.5 text-[9px] font-bold uppercase tracking-wider text-gold">
            Methodology
          </span>
        </div>
        <div className="flex items-center gap-1 text-[11px] text-slate-500">
          <span>{open ? 'Hide' : 'Review assumptions'}</span>
          {open ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
        </div>
      </button>

      {open && (
        <div className="border-t border-[#232a36] px-4 py-3.5 text-xs text-slate-400">
          <ul className="list-inside list-disc space-y-1.5 leading-relaxed">
            {assumptions.map((item, idx) => (
              <li key={idx} className="text-slate-300">
                <span className="text-slate-400">{item}</span>
              </li>
            ))}
          </ul>
          {note && (
            <div className="mt-3 rounded-lg border border-gold/20 bg-gold/5 p-2.5 text-[11px] text-gold/90">
              <span className="font-semibold text-gold">Production Notice: </span>
              {note}
            </div>
          )}
        </div>
      )}
    </div>
  )
}
