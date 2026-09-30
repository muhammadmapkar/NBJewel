export function SampleBadge() {
  return <span className="inline-flex items-center gap-1.5 rounded-full border border-gold/30 bg-gold/10 px-2.5 py-1 text-[10px] font-bold uppercase tracking-[.14em] text-gold"><span className="h-1.5 w-1.5 rounded-full bg-gold" />Sample data</span>
}

export function StatusDot({ status }: { status: 'red' | 'amber' | 'green' }) {
  const styles = { red: 'bg-danger/10 text-danger border-danger/20', amber: 'bg-gold/10 text-gold border-gold/20', green: 'bg-mint/10 text-mint border-mint/20' }
  return <span className={`rounded-full border px-2 py-1 text-[10px] font-bold uppercase tracking-wider ${styles[status]}`}>{status === 'red' ? 'Stock out risk' : status === 'amber' ? 'Watch' : 'Healthy'}</span>
}
