import { useEffect, useRef, useState } from 'react'
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
  Camera,
  CheckCircle,
  Download,
  Gem,
  Layers,
  Search,
  Sparkles,
  Upload,
} from 'lucide-react'
import {
  getDesignSimilarity,
  type DesignSearchResult,
} from '../services/api'
import { DesignSvgThumbnail } from './DesignSvgThumbnail'
import { SampleBadge } from './Badge'
import { AssumptionsPanel } from './AssumptionsPanel'
import { generatedDesigns, type DesignSku } from '../data/designData'

const gold = '#d6a84f'
const grid = '#29313e'
const fmt = new Intl.NumberFormat('en-IN', { maximumFractionDigits: 0 })

function Card({ children, className = '' }: { children: React.ReactNode; className?: string }) {
  return <div className={`glass rounded-2xl p-4 sm:p-5 ${className}`}>{children}</div>
}

function Loading() {
  return (
    <div className="space-y-4">
      <div className="grid gap-4 sm:grid-cols-3">
        {[1, 2, 3].map((x) => (
          <div key={x} className="skeleton h-36 rounded-2xl" />
        ))}
      </div>
      <div className="skeleton h-80 rounded-2xl" />
    </div>
  )
}

export function DesignSearchPage() {
  const [selectedSkuId, setSelectedSkuId] = useState<string>('DSG-3000')
  const [data, setData] = useState<DesignSearchResult | null>(null)
  const [loading, setLoading] = useState(true)
  const [uploadedColorHex, setUploadedColorHex] = useState<string | null>(null)
  const [searchFilter, setSearchFilter] = useState('')
  const fileInputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    setLoading(true)
    getDesignSimilarity(selectedSkuId)
      .then(setData)
      .finally(() => setLoading(false))
  }, [selectedSkuId])

  const handlePhotoUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return

    const reader = new FileReader()
    reader.onload = (event) => {
      const img = new Image()
      img.onload = () => {
        const canvas = document.createElement('canvas')
        const ctx = canvas.getContext('2d')
        canvas.width = 50
        canvas.height = 50
        if (!ctx) return
        ctx.drawImage(img, 0, 0, 50, 50)
        const imgData = ctx.getImageData(0, 0, 50, 50).data

        // Calculate average RGB
        let r = 0, g = 0, b = 0, count = 0
        for (let i = 0; i < imgData.length; i += 4) {
          r += imgData[i]
          g += imgData[i + 1]
          b += imgData[i + 2]
          count++
        }
        const avgR = Math.round(r / count)
        const avgG = Math.round(g / count)
        const avgB = Math.round(b / count)
        const hex = `#${((1 << 24) + (avgR << 16) + (avgG << 8) + avgB).toString(16).slice(1)}`
        setUploadedColorHex(hex)

        // Find design that best matches metal tone or stone tone
        // E.g., if reddish -> pick ruby/rose gold; if yellowish -> yellow gold; greenish -> emerald
        let bestMatch = generatedDesigns[0]
        if (avgR > 160 && avgG < 110) {
          bestMatch = generatedDesigns.find((d) => d.stone === 'Ruby') || generatedDesigns[0]
        } else if (avgG > 140 && avgR < 130) {
          bestMatch = generatedDesigns.find((d) => d.stone === 'Emerald') || generatedDesigns[0]
        } else if (avgR > 180 && avgG > 150 && avgB < 120) {
          bestMatch = generatedDesigns.find((d) => d.metalColour === 'Yellow Gold' && d.stone === 'Polki') || generatedDesigns[0]
        } else {
          bestMatch = generatedDesigns[Math.floor(Math.random() * 10)]
        }
        setSelectedSkuId(bestMatch.sku)
      }
      img.src = event.target?.result as string
    }
    reader.readAsDataURL(file)
  }

  const exportCsv = () => {
    if (!data) return
    const headers = [
      'SKU',
      'Title',
      'Category',
      'Motif',
      'Stone',
      'Metal Colour',
      'Weight Band',
      'Gross Weight (g)',
      'Price (AED)',
      'Similarity Score (%)',
    ]
    const rows = [
      [
        data.selectedSku.sku,
        `"${data.selectedSku.title}"`,
        data.selectedSku.category,
        data.selectedSku.motif,
        data.selectedSku.stone,
        data.selectedSku.metalColour,
        data.selectedSku.weightBand,
        data.selectedSku.grossWeightG,
        data.selectedSku.priceAED,
        '100 (Query Target)',
      ].join(','),
      ...data.similarSkus.map((s) =>
        [
          s.sku,
          `"${s.title}"`,
          s.category,
          s.motif,
          s.stone,
          s.metalColour,
          s.weightBand,
          s.grossWeightG,
          s.priceAED,
          s.similarityPct,
        ].join(',')
      ),
    ]
    const csv = [headers.join(','), ...rows].join('\n')
    const blob = new Blob([csv], { type: 'text/csv' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `similar-designs-${data.selectedSku.sku}.csv`
    a.click()
    URL.revokeObjectURL(url)
  }

  const filteredCatalogue = data?.allSkus.filter((item) => {
    if (!searchFilter) return true
    const q = searchFilter.toLowerCase()
    return (
      item.sku.toLowerCase().includes(q) ||
      item.title.toLowerCase().includes(q) ||
      item.motif.toLowerCase().includes(q) ||
      item.stone.toLowerCase().includes(q) ||
      item.category.toLowerCase().includes(q)
    )
  })

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <div className="mb-3">
            <SampleBadge />
          </div>
          <h1 className="text-2xl font-extrabold tracking-tight sm:text-3xl">
            Design <span className="text-gold">Similarity Search</span>
          </h1>
          <p className="mt-1 text-sm text-slate-500">
            Vector embeddings and cosine similarity across motifs, gemstones, and silhouettes.
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {/* Simulated Photo Upload */}
          <input
            ref={fileInputRef}
            type="file"
            accept="image/*"
            className="hidden"
            onChange={handlePhotoUpload}
          />
          <button
            onClick={() => fileInputRef.current?.click()}
            className="flex h-9 items-center gap-2 rounded-lg border border-gold/40 bg-gold/15 px-3 text-xs font-bold text-gold hover:bg-gold/25"
          >
            <Camera size={14} /> Upload Photo
          </button>

          <button
            onClick={exportCsv}
            className="flex h-9 items-center gap-2 rounded-lg border border-[#303846] bg-[#171d26] px-3 text-xs font-bold text-slate-300 hover:text-white"
          >
            <Download size={14} /> Export CSV
          </button>
        </div>
      </div>

      {uploadedColorHex && (
        <div className="flex items-center justify-between rounded-xl border border-gold/30 bg-gold/10 px-4 py-2.5 text-xs text-gold">
          <div className="flex items-center gap-2">
            <span
              className="h-4 w-4 rounded-full border border-white/40 shadow-sm"
              style={{ backgroundColor: uploadedColorHex }}
            />
            <span>Extracted dominant canvas color: <strong>{uploadedColorHex}</strong></span>
          </div>
          <span className="text-[11px] text-slate-400 italic">
            Simulated in this prototype. Real version uses image embeddings.
          </span>
        </div>
      )}

      {loading || !data ? (
        <Loading />
      ) : (
        <>
          {/* Query Selection & Target Piece Card */}
          <div className="grid gap-4 lg:grid-cols-[380px_1fr]">
            {/* Target Piece Showcase */}
            <Card className="flex flex-col justify-between border-2 border-gold/40 bg-gradient-to-b from-[#181e2b] to-[#10141d]">
              <div>
                <div className="flex items-center justify-between mb-3">
                  <span className="rounded bg-gold/20 px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider text-gold">
                    Query Target Item
                  </span>
                  <span className="font-mono text-xs font-bold text-slate-400">
                    {data.selectedSku.sku}
                  </span>
                </div>

                <div className="flex flex-col items-center py-2">
                  <DesignSvgThumbnail
                    category={data.selectedSku.category}
                    motif={data.selectedSku.motif}
                    stone={data.selectedSku.stone}
                    metalColour={data.selectedSku.metalColour}
                    size={140}
                  />
                  <div className="mt-3 text-center">
                    <h3 className="text-base font-bold text-slate-100">{data.selectedSku.title}</h3>
                    <div className="mt-1 flex items-center justify-center gap-2 text-xs text-slate-400">
                      <span>{data.selectedSku.category}</span>
                      <span>·</span>
                      <span>{data.selectedSku.metalColour}</span>
                      <span>·</span>
                      <span className="font-mono font-bold text-gold">
                        AED {fmt.format(data.selectedSku.priceAED)}
                      </span>
                    </div>
                  </div>
                </div>

                <div className="mt-4 grid grid-cols-2 gap-2 text-xs border-t border-[#232b38] pt-3">
                  <div className="text-slate-500">Motif: <span className="text-slate-200 font-medium">{data.selectedSku.motif}</span></div>
                  <div className="text-slate-500">Stone: <span className="text-slate-200 font-medium">{data.selectedSku.stone}</span></div>
                  <div className="text-slate-500">Weight: <span className="text-slate-200 font-medium">{data.selectedSku.grossWeightG}g</span></div>
                  <div className="text-slate-500">Band: <span className="text-slate-200 font-medium">{data.selectedSku.weightBand}</span></div>
                </div>
              </div>

              {/* SKU Picker Dropdown */}
              <div className="mt-4 pt-3 border-t border-[#232b38]">
                <label className="text-[10px] font-bold uppercase tracking-wider text-slate-500 block mb-1">
                  Change Target Piece
                </label>
                <div className="flex gap-2">
                  <div className="relative flex-1">
                    <input
                      type="text"
                      placeholder="Search SKU or motif..."
                      value={searchFilter}
                      onChange={(e) => setSearchFilter(e.target.value)}
                      className="h-8 w-full rounded-lg border border-[#303846] bg-[#141a24] pl-7 pr-2 text-xs text-slate-200 outline-none focus:border-gold"
                    />
                    <Search size={13} className="absolute left-2.5 top-2.5 text-slate-500" />
                  </div>
                  <select
                    value={data.selectedSku.sku}
                    onChange={(e) => setSelectedSkuId(e.target.value)}
                    className="h-8 max-w-[140px] rounded-lg border border-[#303846] bg-[#141a24] px-2 text-xs text-slate-200 outline-none focus:border-gold"
                  >
                    {filteredCatalogue?.slice(0, 30).map((d) => (
                      <option key={d.sku} value={d.sku}>
                        {d.sku} ({d.category})
                      </option>
                    ))}
                  </select>
                </div>
              </div>
            </Card>

            {/* Top 8 Similar Pieces Grid */}
            <Card>
              <div className="mb-4 flex items-center justify-between">
                <div>
                  <h2 className="font-bold text-slate-100">Top 8 Vector Matches</h2>
                  <p className="text-xs text-slate-500">
                    Calculated via in-browser cosine similarity on attribute embeddings.
                  </p>
                </div>
                <span className="font-mono text-[10px] text-slate-500">COSINE SIMILARITY</span>
              </div>

              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
                {data.similarSkus.map((match) => (
                  <div
                    key={match.sku}
                    onClick={() => setSelectedSkuId(match.sku)}
                    className="group cursor-pointer rounded-xl border border-[#252c38] bg-[#121721] p-3 transition hover:border-gold/50 hover:bg-[#161c28]"
                  >
                    <div className="relative flex items-center justify-center">
                      <DesignSvgThumbnail
                        category={match.category}
                        motif={match.motif}
                        stone={match.stone}
                        metalColour={match.metalColour}
                        size={84}
                      />
                      <span className="absolute top-1 right-1 rounded-md bg-emerald-500/20 px-1.5 py-0.5 font-mono text-[10px] font-bold text-emerald-400">
                        {match.similarityPct}%
                      </span>
                    </div>

                    <div className="mt-2 text-xs">
                      <div className="font-semibold text-slate-200 truncate group-hover:text-gold">
                        {match.title}
                      </div>
                      <div className="flex items-center justify-between text-[11px] text-slate-400 mt-1">
                        <span>{match.sku}</span>
                        <span className="font-mono font-bold text-slate-300">
                          AED {fmt.format(match.priceAED)}
                        </span>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            </Card>
          </div>

          {/* Matching Set Suggestions */}
          <Card>
            <div className="mb-3 flex items-center justify-between">
              <div>
                <div className="flex items-center gap-2">
                  <Sparkles size={16} className="text-gold" />
                  <h2 className="font-bold text-slate-100">Complementary Matching Set Suggestions</h2>
                </div>
                <p className="text-xs text-slate-500">
                  Recommended pairings designed to cross-sell alongside {data.selectedSku.title}.
                </p>
              </div>
              <span className="rounded bg-gold/15 px-2 py-0.5 text-[10px] font-bold text-gold">
                Ensemble Recommender
              </span>
            </div>

            <div className="grid gap-4 sm:grid-cols-3">
              {data.matchingSet.earrings && (
                <div className="flex items-center gap-3 rounded-xl border border-[#27303e] bg-[#111620] p-3">
                  <DesignSvgThumbnail
                    category={data.matchingSet.earrings.category}
                    motif={data.matchingSet.earrings.motif}
                    stone={data.matchingSet.earrings.stone}
                    metalColour={data.matchingSet.earrings.metalColour}
                    size={64}
                  />
                  <div className="flex-1 text-xs">
                    <span className="text-[10px] uppercase font-bold text-gold">Matching Earrings</span>
                    <div className="font-bold text-slate-200">{data.matchingSet.earrings.title}</div>
                    <div className="text-slate-400 font-mono mt-0.5">AED {fmt.format(data.matchingSet.earrings.priceAED)}</div>
                  </div>
                </div>
              )}

              {data.matchingSet.bangles && (
                <div className="flex items-center gap-3 rounded-xl border border-[#27303e] bg-[#111620] p-3">
                  <DesignSvgThumbnail
                    category={data.matchingSet.bangles.category}
                    motif={data.matchingSet.bangles.motif}
                    stone={data.matchingSet.bangles.stone}
                    metalColour={data.matchingSet.bangles.metalColour}
                    size={64}
                  />
                  <div className="flex-1 text-xs">
                    <span className="text-[10px] uppercase font-bold text-gold">Matching Bangles</span>
                    <div className="font-bold text-slate-200">{data.matchingSet.bangles.title}</div>
                    <div className="text-slate-400 font-mono mt-0.5">AED {fmt.format(data.matchingSet.bangles.priceAED)}</div>
                  </div>
                </div>
              )}

              {data.matchingSet.rings && (
                <div className="flex items-center gap-3 rounded-xl border border-[#27303e] bg-[#111620] p-3">
                  <DesignSvgThumbnail
                    category={data.matchingSet.rings.category}
                    motif={data.matchingSet.rings.motif}
                    stone={data.matchingSet.rings.stone}
                    metalColour={data.matchingSet.rings.metalColour}
                    size={64}
                  />
                  <div className="flex-1 text-xs">
                    <span className="text-[10px] uppercase font-bold text-gold">Matching Ring</span>
                    <div className="font-bold text-slate-200">{data.matchingSet.rings.title}</div>
                    <div className="text-slate-400 font-mono mt-0.5">AED {fmt.format(data.matchingSet.rings.priceAED)}</div>
                  </div>
                </div>
              )}
            </div>
          </Card>

          {/* Design DNA Panel */}
          <Card>
            <div className="mb-4">
              <h2 className="font-bold text-slate-100">Design DNA: Commercial Sell-Through Performance</h2>
              <p className="text-xs text-slate-500">
                Average sell-through percentage achieved across attributes in the design repository.
              </p>
            </div>

            <div className="grid gap-6 lg:grid-cols-3">
              {/* By Motif */}
              <div>
                <h3 className="text-xs font-bold text-slate-400 mb-2 uppercase tracking-wider">
                  Sell-Through by Motif
                </h3>
                <ResponsiveContainer width="100%" height={190}>
                  <BarChart data={data.dnaByMotif} margin={{ left: -10, right: 10 }}>
                    <CartesianGrid stroke={grid} vertical={false} />
                    <XAxis dataKey="attribute" stroke="#687386" fontSize={10} interval={0} />
                    <YAxis stroke="#687386" fontSize={10} unit="%" domain={[0, 100]} />
                    <Tooltip
                      content={({ active, payload, label }) => {
                        if (active && payload && payload.length) {
                          return (
                            <div className="rounded-lg border border-[#343e4d] bg-[#11161e] p-2.5 text-xs shadow-xl">
                              <div className="font-bold text-slate-200">{label}</div>
                              <div className="text-gold font-semibold">
                                Sell-through: {payload[0].value}%
                              </div>
                            </div>
                          )
                        }
                        return null
                      }}
                    />
                    <Bar dataKey="avgSellThroughPct" fill={gold} radius={[4, 4, 0, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              </div>

              {/* By Stone */}
              <div>
                <h3 className="text-xs font-bold text-slate-400 mb-2 uppercase tracking-wider">
                  Sell-Through by Gemstone
                </h3>
                <ResponsiveContainer width="100%" height={190}>
                  <BarChart data={data.dnaByStone} margin={{ left: -10, right: 10 }}>
                    <CartesianGrid stroke={grid} vertical={false} />
                    <XAxis dataKey="attribute" stroke="#687386" fontSize={10} interval={0} />
                    <YAxis stroke="#687386" fontSize={10} unit="%" domain={[0, 100]} />
                    <Tooltip
                      content={({ active, payload, label }) => {
                        if (active && payload && payload.length) {
                          return (
                            <div className="rounded-lg border border-[#343e4d] bg-[#11161e] p-2.5 text-xs shadow-xl">
                              <div className="font-bold text-slate-200">{label}</div>
                              <div className="text-emerald-400 font-semibold">
                                Sell-through: {payload[0].value}%
                              </div>
                            </div>
                          )
                        }
                        return null
                      }}
                    />
                    <Bar dataKey="avgSellThroughPct" fill="#10b981" radius={[4, 4, 0, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              </div>

              {/* By Weight Band */}
              <div>
                <h3 className="text-xs font-bold text-slate-400 mb-2 uppercase tracking-wider">
                  Sell-Through by Weight Band
                </h3>
                <ResponsiveContainer width="100%" height={190}>
                  <BarChart data={data.dnaByWeightBand} margin={{ left: -10, right: 10 }}>
                    <CartesianGrid stroke={grid} vertical={false} />
                    <XAxis dataKey="attribute" stroke="#687386" fontSize={10} interval={0} />
                    <YAxis stroke="#687386" fontSize={10} unit="%" domain={[0, 100]} />
                    <Tooltip
                      content={({ active, payload, label }) => {
                        if (active && payload && payload.length) {
                          return (
                            <div className="rounded-lg border border-[#343e4d] bg-[#11161e] p-2.5 text-xs shadow-xl">
                              <div className="font-bold text-slate-200">{label}</div>
                              <div className="text-sky-400 font-semibold">
                                Sell-through: {payload[0].value}%
                              </div>
                            </div>
                          )
                        }
                        return null
                      }}
                    />
                    <Bar dataKey="avgSellThroughPct" fill="#0284c7" radius={[4, 4, 0, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </div>
          </Card>

          {/* Assumptions Collapsible Panel */}
          <AssumptionsPanel
            title="Design Embedding and Similarity Assumptions"
            assumptions={[
              'Vector Embedding Composition: Each design SKU vector is composed of normalized attribute one-hot encodings (category, motif, stone, metal colour, weight band) combined with latent dimensional weights.',
              'Cosine Similarity In Browser: Vector similarity is evaluated dynamically in JavaScript using the dot product of normalized 16-dimensional embedding vectors.',
              'Procedural SVG Rendering: SVG previews are rendered dynamically on the client without external static image assets, ensuring high performance and offline reliability.',
              'Photo Upload Matching: Image upload extracts canvas dominant RGB and matches with catalogue attributes. Production replaces this with ResNet/CLIP jewellery visual embeddings.',
            ]}
            note="Simulated in this prototype. Real enterprise version connects to multimodal jewellery embedding services (CLIP / ResNet) indexing CAD models and photography."
          />
        </>
      )}
    </div>
  )
}
