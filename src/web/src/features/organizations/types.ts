// Mirrors the Api records in src/BuildYourOwnAI.Api/Features/{Organizations,Documents}.

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

export type DocumentChunks = {
  documentId: string
  fileName: string
  chunkCount: number
  chunks: { index: number; content: string }[]
}
