import { festivals } from '../config/festivals'

export const STORES = ['Dubai Deira', 'Sharjah', 'Chennai T Nagar'] as const
export const CATEGORIES = ['Rings', 'Necklaces', 'Bangles', 'Earrings', 'Coins & Bars'] as const
export const PURITIES = ['18K', '22K', '24K'] as const
export type Store = (typeof STORES)[number]
export type Category = (typeof CATEGORIES)[number]

export type Customer = {
  id: number
  name: string
  phone: string
  goldScheme: boolean
}
export type Sale = {
  date: string
  store: Store
  category: Category
  purity: (typeof PURITIES)[number]
  grams: number
  value: number
  customerId: number
}
export type DailyMetric = { date: string; sales: number; grams: number; value: number; goldRate: number }

type Rng = () => number
const makeRng = (seed: number): Rng => {
  let state = seed >>> 0
  return () => {
    state = (1664525 * state + 1013904223) >>> 0
    return state / 4294967296
  }
}
const names = ['Aarav', 'Aisha', 'Arjun', 'Diya', 'Ishaan', 'Kavya', 'Maya', 'Neha', 'Rohan', 'Sara', 'Vikram', 'Zoya', 'Aditya', 'Ananya', 'Kabir', 'Meera']
const surnames = ['Sharma', 'Patel', 'Nair', 'Khan', 'Iyer', 'Mehta', 'Reddy', 'Menon', 'Bhatia', 'Pillai', 'Kapoor', 'Thomas']
const iso = (date: Date) => date.toISOString().slice(0, 10)
const daysBetween = (a: Date, b: Date) => Math.round((b.getTime() - a.getTime()) / 86400000)

export const generated = (() => {
  const random = makeRng(20260928)
  const end = new Date('2026-09-28T00:00:00Z')
  const start = new Date('2023-09-29T00:00:00Z')
  const customers: Customer[] = Array.from({ length: 2000 }, (_, i) => {
    const digits = String(10000000 + Math.floor(random() * 89999999))
    return { id: i + 1, name: `${names[i % names.length]} ${surnames[(i * 7) % surnames.length]}`, phone: `+91 •••• ${digits.slice(-4)}`, goldScheme: random() < 0.28 }
  })
  const sales: Sale[] = []
  const daily: DailyMetric[] = []
  const festivalDates = festivals.map((f) => new Date(f.date))
  for (let cursor = new Date(start); cursor <= end; cursor.setUTCDate(cursor.getUTCDate() + 1)) {
    const date = iso(cursor)
    const day = daysBetween(start, cursor)
    const seasonal = 1 + Math.max(0, Math.sin((day / 365.25) * Math.PI * 2 - 1.1)) * 0.18
    const festivalBoost = festivalDates.reduce((boost, festivalDate) => {
      const distance = Math.abs(daysBetween(cursor, festivalDate))
      return boost + (distance <= 10 ? (11 - distance) * 0.045 : 0)
    }, 0)
    const count = Math.max(3, Math.floor((4.2 + random() * 7) * seasonal * (1 + festivalBoost)))
    let dayGrams = 0
    let dayValue = 0
    for (let saleIndex = 0; saleIndex < count; saleIndex++) {
      const category = CATEGORIES[Math.floor(random() * CATEGORIES.length)]
      const purity = PURITIES[Math.floor(random() * PURITIES.length)]
      const grams = Number((2.5 + random() * (category === 'Coins & Bars' ? 35 : 18)).toFixed(2))
      const rate = 5800 + day * 2.05 + Math.sin(day / 40) * 90 + random() * 80
      const value = Math.round(grams * rate * (purity === '24K' ? 1.08 : purity === '22K' ? 1.02 : 0.94))
      sales.push({ date, store: STORES[Math.floor(random() * STORES.length)], category, purity, grams, value, customerId: customers[Math.floor(random() * customers.length)].id })
      dayGrams += grams
      dayValue += value
    }
    daily.push({ date, sales: count, grams: Number(dayGrams.toFixed(2)), value: dayValue, goldRate: Math.round(5800 + day * 2.05 + Math.sin(day / 40) * 90) })
  }
  return { customers, sales, daily }
})()

export const getCustomer = (id: number) => generated.customers[id - 1]
export const getFestivalDate = (name: string, year: number) => festivals.find((f) => f.name === name && f.date.startsWith(String(year)))
