import { useOutletContext } from 'react-router'
import type { OrganizationDetail } from './types'

/** The organization the tabs under `/organizations/:id` belong to, loaded once by its layout. */
export const useCurrentOrganization = () => useOutletContext<OrganizationDetail>()
