// Shapes more than one feature reads. Mirrors src/BuildYourOwnAI.Api/Common/AskPipeline.cs.

export type Source = { documentId: string; fileName: string; chunkIndex: number; excerpt: string }

export type AskResponse = { answer: string; found: boolean; sources: Source[] }
