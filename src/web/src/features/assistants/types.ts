// Mirrors the Api records in src/BuildYourOwnAI.Api/Features/Assistants.

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
