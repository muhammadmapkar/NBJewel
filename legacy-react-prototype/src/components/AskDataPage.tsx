import { useState } from 'react'
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
  CartesianGrid,
} from 'recharts'
import {
  Bot,
  CornerDownLeft,
  Download,
  HelpCircle,
  MessageSquare,
  Sparkles,
  Terminal,
  User,
} from 'lucide-react'
import { matchQuestionIntent, metricRegistry, type MetricDefinition } from '../assistant/metricRegistry'
import { SampleBadge } from './Badge'
import { AssumptionsPanel } from './AssumptionsPanel'

const gold = '#d6a84f'
const grid = '#29313e'

interface ChatMessage {
  id: string
  sender: 'user' | 'assistant'
  text: string
  timestamp: string
  metricUsed?: string
  data?: any
  chartType?: 'bar' | 'line' | 'pie'
  columns?: { key: string; label: string }[]
}

const exampleChips = [
  'Which store lost most fine gold last week?',
  'Which karigar is drifting above normal loss?',
  'How many high severity compliance flags?',
  'How much can we recover from aged stock?',
  'Festival demand forecast by category?',
  'How many customers are at risk?',
  'Find similar designs for our hero necklace',
]

// Real version: an LLM picks a metric from this registry via tool calling. It never writes free SQL.

export function AskDataPage() {
  const [messages, setMessages] = useState<ChatMessage[]>([
    {
      id: 'init',
      sender: 'assistant',
      text: 'Hello. I am your NB Jewel intelligence assistant. Ask me questions about store fine gold variances, karigar workshop loss drift, customer RFM churn, compliance exceptions, or aged stock recovery.',
      timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
    },
  ])
  const [input, setInput] = useState('')
  const [loading, setLoading] = useState(false)

  const handleSend = (textToSend?: string) => {
    const question = (textToSend ?? input).trim()
    if (!question) return

    const userMsg: ChatMessage = {
      id: `usr-${Date.now()}`,
      sender: 'user',
      text: question,
      timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
    }

    setMessages((prev) => [...prev, userMsg])
    setInput('')
    setLoading(true)

    setTimeout(() => {
      const match = matchQuestionIntent(question)

      if (match.metric) {
        const result = match.metric.execute(match.queryParams)
        const botMsg: ChatMessage = {
          id: `bot-${Date.now()}`,
          sender: 'assistant',
          text: result.answer,
          timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
          metricUsed: result.metricUsed,
          data: result.data,
          chartType: result.chartType,
          columns: result.columns,
        }
        setMessages((prev) => [...prev, botMsg])
      } else {
        const botMsg: ChatMessage = {
          id: `bot-${Date.now()}`,
          sender: 'assistant',
          text: 'I could not find a verified analytical metric matching your query. To ensure mathematical accuracy and prevent SQL hallucinations, please choose from the supported questions below:',
          timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
          columns: undefined,
        }
        setMessages((prev) => [...prev, botMsg])
      }
      setLoading(false)
    }, 350)
  }

  const exportCsv = () => {
    // Export conversation transcript
    const headers = ['Timestamp', 'Sender', 'Text', 'Metric Used']
    const rows = messages.map((m) =>
      [
        `"${m.timestamp}"`,
        m.sender,
        `"${m.text.replace(/"/g, '""')}"`,
        m.metricUsed ? `"${m.metricUsed}"` : '""',
      ].join(',')
    )
    const csv = [headers.join(','), ...rows].join('\n')
    const blob = new Blob([csv], { type: 'text/csv' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `ask-data-transcript.csv`
    a.click()
    URL.revokeObjectURL(url)
  }

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <div className="mb-3">
            <SampleBadge />
          </div>
          <h1 className="text-2xl font-extrabold tracking-tight sm:text-3xl">
            Ask Your Data <span className="text-gold">(AI Assistant)</span>
          </h1>
          <p className="mt-1 text-sm text-slate-500">
            Deterministic metric-registry tool calling for jewelry retail operations. Zero SQL hallucinations.
          </p>
        </div>

        <button
          onClick={exportCsv}
          className="flex h-9 items-center gap-2 rounded-lg border border-gold/30 bg-gold/10 px-3 text-xs font-bold text-gold hover:bg-gold/20"
        >
          <Download size={14} /> Export Transcript CSV
        </button>
      </div>

      {/* Suggested Prompt Chips */}
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-[11px] font-bold uppercase tracking-wider text-slate-500 flex items-center gap-1">
          <Sparkles size={12} className="text-gold" /> Suggested:
        </span>
        {exampleChips.map((chip, idx) => (
          <button
            key={idx}
            onClick={() => handleSend(chip)}
            className="rounded-full border border-[#2b3442] bg-[#141a24] px-3 py-1 text-xs text-slate-300 transition hover:border-gold/50 hover:bg-[#1a2230] hover:text-white"
          >
            {chip}
          </button>
        ))}
      </div>

      {/* Main Chat Window */}
      <div className="glass flex h-[580px] flex-col rounded-2xl border border-[#27303d] overflow-hidden">
        {/* Chat Feed */}
        <div className="scrollbar flex-1 space-y-4 overflow-y-auto p-4 sm:p-6">
          {messages.map((msg) => (
            <div
              key={msg.id}
              className={`flex gap-3 ${msg.sender === 'user' ? 'justify-end' : 'justify-start'}`}
            >
              {msg.sender === 'assistant' && (
                <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-xl bg-gold/15 text-gold border border-gold/30">
                  <Bot size={16} />
                </div>
              )}

              <div
                className={`max-w-[85%] rounded-2xl p-4 text-xs leading-relaxed ${
                  msg.sender === 'user'
                    ? 'bg-gold text-slate-950 font-semibold'
                    : 'border border-[#2a3342] bg-[#121722] text-slate-200'
                }`}
              >
                <div className="flex items-center justify-between gap-4 mb-1">
                  <span className={`text-[10px] font-bold uppercase tracking-wider ${msg.sender === 'user' ? 'text-slate-800' : 'text-slate-500'}`}>
                    {msg.sender === 'user' ? 'You' : 'NB Jewel Assistant'}
                  </span>
                  <span className={`text-[10px] ${msg.sender === 'user' ? 'text-slate-800' : 'text-slate-500'}`}>
                    {msg.timestamp}
                  </span>
                </div>

                <div className="text-sm">{msg.text}</div>

                {/* Render Table payload if present */}
                {msg.data && msg.columns && (
                  <div className="mt-3 overflow-x-auto rounded-lg border border-[#222935] bg-[#0c1017] p-2.5">
                    <table className="w-full text-left text-xs">
                      <thead className="border-b border-[#202733] text-[10px] uppercase text-slate-500">
                        <tr>
                          {msg.columns.map((c) => (
                            <th key={c.key} className="pb-1.5 pr-3">{c.label}</th>
                          ))}
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-[#171d26]">
                        {msg.data.map((row: any, i: number) => (
                          <tr key={i} className="text-slate-300">
                            {msg.columns!.map((c) => (
                              <td key={c.key} className="py-1.5 pr-3 font-medium">
                                {row[c.key]}
                              </td>
                            ))}
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}

                {/* Render Chart payload if present */}
                {msg.data && msg.chartType === 'bar' && !msg.columns && (
                  <div className="mt-3 rounded-lg border border-[#222935] bg-[#0c1017] p-3">
                    <ResponsiveContainer width="100%" height={150}>
                      <BarChart data={msg.data} margin={{ top: 5, right: 10, left: -20, bottom: 0 }}>
                        <CartesianGrid stroke={grid} vertical={false} />
                        <XAxis dataKey="segment" stroke="#687386" fontSize={10} />
                        <YAxis stroke="#687386" fontSize={10} />
                        <Tooltip />
                        <Bar dataKey="count" fill={gold} radius={[4, 4, 0, 0]} />
                      </BarChart>
                    </ResponsiveContainer>
                  </div>
                )}

                {/* Footer Line with exact metric used */}
                {msg.metricUsed && (
                  <div className="mt-3 pt-2 border-t border-[#222a36] text-[10px] font-mono text-slate-500 flex items-center gap-1.5">
                    <Terminal size={11} className="text-gold" />
                    <span>Metric used: <strong>{msg.metricUsed}</strong></span>
                  </div>
                )}
              </div>

              {msg.sender === 'user' && (
                <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-xl bg-slate-800 text-slate-300 border border-slate-700">
                  <User size={16} />
                </div>
              )}
            </div>
          ))}

          {loading && (
            <div className="flex items-center gap-3">
              <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-xl bg-gold/15 text-gold border border-gold/30">
                <Bot size={16} />
              </div>
              <div className="rounded-2xl border border-[#2a3342] bg-[#121722] px-4 py-3 text-xs text-slate-400">
                <span className="inline-flex items-center gap-1">
                  Querying metric registry...
                  <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-gold" />
                </span>
              </div>
            </div>
          )}
        </div>

        {/* Input Bar */}
        <div className="border-t border-[#232b38] bg-[#0d1117] p-3 sm:p-4">
          <form
            onSubmit={(e) => {
              e.preventDefault()
              handleSend()
            }}
            className="flex items-center gap-2"
          >
            <input
              type="text"
              value={input}
              onChange={(e) => setInput(e.target.value)}
              placeholder="Ask about fine gold variance, drifting karigars, compliance flags, or aged stock..."
              className="h-10 flex-1 rounded-xl border border-[#303846] bg-[#141a24] px-4 text-xs text-slate-200 outline-none focus:border-gold placeholder:text-slate-500"
            />
            <button
              type="submit"
              disabled={!input.trim()}
              className="flex h-10 items-center gap-1.5 rounded-xl bg-gold px-4 text-xs font-bold text-slate-950 transition hover:bg-gold/90 disabled:opacity-40"
            >
              <span>Ask</span>
              <CornerDownLeft size={14} />
            </button>
          </form>
        </div>
      </div>

      {/* Assumptions Collapsible Panel */}
      <AssumptionsPanel
        title="AI Assistant Architectural Model Assumptions"
        assumptions={[
          'Metric Registry Determinism: The AI assistant routes natural language questions exclusively into predefined, audited analytic functions. It never generates raw arbitrary SQL against operational databases.',
          'Natural Language Intent Matching: In this prototype, queries are scored via keyword and synonym weighting. Production replaces this with an LLM function/tool-calling orchestrator.',
          'Result Guardrails: Numerical answers, tables, and charts are generated strictly by executing deterministic TypeScript / C# business logic functions.',
        ]}
        note="Real version: an LLM picks a metric from this registry via tool calling. It never writes free SQL."
      />
    </div>
  )
}
