// Mirrors the Api records in src/BuildYourOwnAI.Api/Features/{Assistants,Documents,Ask}.

export type AssistantSummary = {
  id: string
  name: string
  instructions: string | null
  createdAt: string
  documentCount: number
}

export type CreateAssistantRequest = { name: string; instructions: string | null }

export type DocumentItem = {
  id: string
  fileName: string
  sizeBytes: number
  chunkCount: number
  uploadedAt: string
}

export type Source = { documentId: string; fileName: string; chunkIndex: number; excerpt: string }

export type AskResponse = { answer: string; sources: Source[] }
