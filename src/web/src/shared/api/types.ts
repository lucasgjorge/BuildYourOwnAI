// Mirrors the Api records in src/BuildYourOwnAI.Api/Features/{Organizations,Documents,Assistants,Ask,Jev,Gaps}.

export type OrganizationSummary = {
  id: string
  name: string
  createdAt: string
  assistantCount: number
  documentCount: number
}

export type OrganizationAssistant = { id: string; name: string; routingDescription: string | null }

export type OrganizationDetail = {
  id: string
  name: string
  createdAt: string
  documentCount: number
  assistants: OrganizationAssistant[]
}

export type DocumentItem = {
  id: string
  fileName: string
  sizeBytes: number
  chunkCount: number
  uploadedAt: string
}

export type Assistant = {
  id: string
  organizationId: string
  organizationName: string
  name: string
  instructions: string | null
  routingDescription: string | null
  createdAt: string
}

export type CreateAssistantRequest = {
  organizationId: string
  name: string
  instructions: string | null
  routingDescription: string | null
}

export type Source = { documentId: string; fileName: string; chunkIndex: number; excerpt: string }

export type AskResponse = { answer: string; found: boolean; sources: Source[] }

export type AssistantRef = { id: string; name: string; organizationName: string }

export type JevResponse =
  | { kind: 'answered'; assistant: AssistantRef; answer: string; found: boolean; sources: Source[]; alternatives: AssistantRef[] }
  | { kind: 'clarify'; candidates: AssistantRef[] }
  | { kind: 'noMatch' }

export type Ref = { id: string; name: string }

export type Gap = {
  id: string
  question: string
  askCount: number
  firstAskedAt: string
  lastAskedAt: string
  organization: Ref | null
  assistant: Ref | null
}
