import { useQuery } from '@tanstack/react-query'
import { getHealth } from '../api/client'

export function HealthBadge() {
  const { data, isPending, isError } = useQuery({
    queryKey: ['health'],
    queryFn: () => getHealth(),
    refetchInterval: 15_000,
  })

  let tone: 'ok' | 'warn' | 'bad' = 'warn'
  let text = 'Checking API…'

  if (isError) {
    tone = 'bad'
    text = 'API unreachable'
  } else if (!isPending && data) {
    const dbOk = data.database === 'connected'
    tone = dbOk ? 'ok' : 'warn'
    text = `API healthy · Database ${data.database ?? 'unknown'}`
  }

  return (
    <div className={`health health-${tone}`} role="status">
      <span className="dot" />
      <span>{text}</span>
    </div>
  )
}
