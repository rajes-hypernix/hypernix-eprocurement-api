import { VendorMaster } from './VendorMaster'
import { VendorDetail } from './VendorDetail'
import { ManualVendorForm } from './ManualVendorForm'
import { NewVendorChooser } from '../onboarding/NewVendorChooser'

export function VendorsPage({
  route,
  onNavigate,
}: {
  route: string
  onNavigate: (key: string) => void
}) {
  if (route === 'vendors/new') {
    return (
      <NewVendorChooser
        onManual={() => onNavigate('vendors/manual')}
        onInvite={() => onNavigate('onboarding/invite')}
        onBack={() => onNavigate('vendors')}
      />
    )
  }
  if (route === 'vendors/manual') {
    return <ManualVendorForm onSaved={(id) => onNavigate(`vendors/${id}`)} onBack={() => onNavigate('vendors/new')} />
  }
  const openId = route.startsWith('vendors/') ? route.slice('vendors/'.length) : null
  return openId ? (
    <VendorDetail id={openId} onBack={() => onNavigate('vendors')} />
  ) : (
    <VendorMaster onOpen={(id) => onNavigate(`vendors/${id}`)} onNavigate={onNavigate} />
  )
}
