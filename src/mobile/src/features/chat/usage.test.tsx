import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen } from '@testing-library/react-native'
import { fakeApi } from '../../test/api'
import { UsageModal } from './UsageModal'

const usage = { inputTokens: 1200, outputTokens: 345, totalTokens: 1545 }
const route = 'GET /api/usage/conversations/c-1'

async function renderModal({ visible = true, onClose = jest.fn() } = {}) {
  await render(
    <QueryClientProvider client={new QueryClient()}>
      <UsageModal conversationId="c-1" visible={visible} onClose={onClose} />
    </QueryClientProvider>,
  )
  return onClose
}

// CU1
it('usage modal shows the conversation totals', async () => {
  fakeApi({ [route]: { status: 200, body: usage } })
  await renderModal()

  expect(await screen.findByText('Uso de tokens')).toBeTruthy()
  expect(screen.getByText('Consumo nesta conversa')).toBeTruthy()
  expect(await screen.findByText('1.545')).toBeTruthy()
  expect(screen.getByText('Total de tokens')).toBeTruthy()
  expect(screen.getByText('1.200')).toBeTruthy()
  expect(screen.getByText('345')).toBeTruthy()
})

// CU2
it('usage modal does not ask the Api while closed', async () => {
  const api = fakeApi({ [route]: { status: 200, body: usage } })
  await renderModal({ visible: false })

  expect(screen.queryByText('Uso de tokens')).toBeNull()
  expect(api).not.toHaveBeenCalled()
})

// CU3
it('usage modal shows loading while the Api answers', async () => {
  fakeApi({ [route]: 'never' })
  await renderModal()

  expect(await screen.findByText('Carregando uso…')).toBeTruthy()
})

// CU4
it('usage modal shows the error title', async () => {
  fakeApi({ [route]: { status: 404, body: { title: 'Conversa não encontrada.' } } })
  await renderModal()

  expect(await screen.findByText('Conversa não encontrada.')).toBeTruthy()
})

// CU5
it('usage modal closes on request', async () => {
  fakeApi({ [route]: { status: 200, body: usage } })
  const onClose = await renderModal()

  await fireEvent.press(await screen.findByRole('button', { name: 'Fechar uso de tokens' }))

  expect(onClose).toHaveBeenCalledTimes(1)
})
