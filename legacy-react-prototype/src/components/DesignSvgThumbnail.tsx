import type { Category } from '../data/generateData'
import type { MetalColour, Motif, Stone } from '../data/designData'

interface SvgThumbnailProps {
  category: Category
  motif: Motif
  stone: Stone
  metalColour: MetalColour
  size?: number
  className?: string
}

export function DesignSvgThumbnail({
  category,
  motif,
  stone,
  metalColour,
  size = 100,
  className = '',
}: SvgThumbnailProps) {
  // Metal colors
  const metalFill =
    metalColour === 'Yellow Gold'
      ? '#d6a84f'
      : metalColour === 'Rose Gold'
        ? '#df8f7e'
        : metalColour === 'White Gold'
          ? '#cbd5e1'
          : '#e0b86a' // Two-tone primary

  const metalAccent =
    metalColour === 'Two-Tone'
      ? '#e2e8f0'
      : metalColour === 'Rose Gold'
        ? '#fbcfe8'
        : metalColour === 'White Gold'
          ? '#94a3b8'
          : '#b58832'

  // Gemstone colors
  const stoneFill =
    stone === 'Ruby'
      ? '#e11d48'
      : stone === 'Emerald'
        ? '#059669'
        : stone === 'Polki'
          ? '#fef08a'
          : stone === 'Kundan'
            ? '#f59e0b'
            : stone === 'CZ'
              ? '#38bdf8'
              : 'none'

  const hasStone = stone !== 'None'

  return (
    <div
      className={`relative flex items-center justify-center overflow-hidden rounded-xl border border-[#2b3340] bg-gradient-to-b from-[#191f2b] to-[#0f131a] ${className}`}
      style={{ width: size, height: size }}
    >
      <svg
        viewBox="0 0 100 100"
        className="h-full w-full p-2"
        fill="none"
        xmlns="http://www.w3.org/2000/svg"
      >
        <defs>
          <radialGradient id={`glow-${motif}-${stone}`} cx="50%" cy="50%" r="50%">
            <stop offset="0%" stopColor={metalFill} stopOpacity="0.25" />
            <stop offset="100%" stopColor="#0c0f14" stopOpacity="0" />
          </radialGradient>
        </defs>

        {/* Subtle background glow */}
        <circle cx="50" cy="50" r="45" fill={`url(#glow-${motif}-${stone})`} />

        {/* Category-specific shapes */}
        {category === 'Rings' && (
          <g>
            <circle cx="50" cy="58" r="24" stroke={metalFill} strokeWidth="6" strokeLinecap="round" />
            <circle cx="50" cy="58" r="21" stroke={metalAccent} strokeWidth="1" strokeDasharray="2 3" />
            {/* Setting crown */}
            <path d="M42 34 L58 34 L54 44 L46 44 Z" fill={metalAccent} />
            {hasStone && (
              <polygon points="50,22 58,32 50,42 42,32" fill={stoneFill} stroke="#ffffff" strokeWidth="0.8" />
            )}
          </g>
        )}

        {category === 'Necklaces' && (
          <g>
            {/* Chain arc */}
            <path
              d="M20 25 C30 75, 70 75, 80 25"
              stroke={metalFill}
              strokeWidth="4"
              fill="none"
              strokeLinecap="round"
            />
            <path
              d="M26 30 C34 68, 66 68, 74 30"
              stroke={metalAccent}
              strokeWidth="1"
              fill="none"
              strokeDasharray="2 3"
            />
            {/* Pendant centerpiece */}
            {motif === 'Peacock' ? (
              <path d="M46 60 Q50 48 54 60 Q62 66 50 78 Q38 66 46 60 Z" fill={metalFill} />
            ) : motif === 'Temple' ? (
              <polygon points="50,50 60,60 56,76 44,76 40,60" fill={metalFill} />
            ) : (
              <circle cx="50" cy="65" r="9" fill={metalFill} />
            )}
            {hasStone && (
              <circle cx="50" cy="65" r="5" fill={stoneFill} stroke="#ffffff" strokeWidth="0.8" />
            )}
          </g>
        )}

        {category === 'Bangles' && (
          <g>
            <circle cx="50" cy="50" r="32" stroke={metalFill} strokeWidth="7" />
            <circle cx="50" cy="50" r="28" stroke={metalAccent} strokeWidth="1.5" strokeDasharray="3 3" />
            {/* Stud embellishments */}
            {hasStone && (
              <>
                <circle cx="50" cy="18" r="3.5" fill={stoneFill} />
                <circle cx="50" cy="82" r="3.5" fill={stoneFill} />
                <circle cx="18" cy="50" r="3.5" fill={stoneFill} />
                <circle cx="82" cy="50" r="3.5" fill={stoneFill} />
              </>
            )}
          </g>
        )}

        {category === 'Earrings' && (
          <g>
            {/* Pair of drop earrings */}
            <circle cx="34" cy="28" r="3.5" fill={metalAccent} />
            <line x1="34" y1="31" x2="34" y2="44" stroke={metalFill} strokeWidth="2" />
            <path d="M28 44 C28 44, 40 44, 34 68 Z" fill={metalFill} />
            {hasStone && <circle cx="34" cy="54" r="3" fill={stoneFill} />}

            <circle cx="66" cy="28" r="3.5" fill={metalAccent} />
            <line x1="66" y1="31" x2="66" y2="44" stroke={metalFill} strokeWidth="2" />
            <path d="M60 44 C60 44, 72 44, 66 68 Z" fill={metalFill} />
            {hasStone && <circle cx="66" cy="54" r="3" fill={stoneFill} />}
          </g>
        )}

        {category === 'Coins & Bars' && (
          <g>
            {motif === 'Temple' ? (
              // Gold Biscuit / Bar
              <>
                <rect x="25" y="25" width="50" height="50" rx="6" fill={metalFill} stroke={metalAccent} strokeWidth="2" />
                <rect x="30" y="30" width="40" height="40" rx="3" fill="none" stroke={metalAccent} strokeWidth="1" strokeDasharray="2 2" />
                <text x="50" y="52" fill="#141820" fontSize="10" fontWeight="900" textAnchor="middle">999.9</text>
                <text x="50" y="62" fill="#141820" fontSize="7" fontWeight="bold" textAnchor="middle">NB JEWEL</text>
              </>
            ) : (
              // Gold Coin
              <>
                <circle cx="50" cy="50" r="32" fill={metalFill} stroke={metalAccent} strokeWidth="3" />
                <circle cx="50" cy="50" r="28" stroke="#ffffff" strokeWidth="0.8" strokeOpacity="0.4" strokeDasharray="3 3" />
                <circle cx="50" cy="50" r="18" fill="none" stroke={metalAccent} strokeWidth="1.5" />
                <text x="50" y="54" fill="#141820" fontSize="12" fontWeight="900" textAnchor="middle">24K</text>
              </>
            )}
          </g>
        )}

        {/* Motif details overlay */}
        {motif === 'Floral' && category !== 'Coins & Bars' && (
          <g transform="translate(50, 50) scale(0.6) translate(-50, -50)">
            <circle cx="50" cy="42" r="4" fill={metalAccent} opacity="0.8" />
            <circle cx="58" cy="50" r="4" fill={metalAccent} opacity="0.8" />
            <circle cx="50" cy="58" r="4" fill={metalAccent} opacity="0.8" />
            <circle cx="42" cy="50" r="4" fill={metalAccent} opacity="0.8" />
          </g>
        )}
      </svg>
    </div>
  )
}
