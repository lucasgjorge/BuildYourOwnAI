import { screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { renderApp } from '../../test/render'
import { anonymous, deferred, problem, server } from '../../test/server'

describe('session guard', () => {
  // C29
  it.each(['/assistants', '/assistants/abc'])('redirects to login without session (%s)', async path => {
    server.use(anonymous())

    renderApp(path)

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/login'))
    expect(screen.getByRole('heading', { name: 'Entrar' })).toBeInTheDocument()
  })

  // C29
  it('redirects to login without session, but /register opens directly', async () => {
    server.use(anonymous())

    renderApp('/register')

    expect(await screen.findByRole('heading', { name: 'Criar conta' })).toBeInTheDocument()
    expect(screen.getByTestId('location')).toHaveTextContent('/register')
  })
})

/** Session is anonymous until a login succeeds, like the real Api. */
function sessionAfterLogin() {
  let loggedIn = false
  return {
    markLoggedIn: () => { loggedIn = true },
    handlers: [
      http.get('*/api/auth/manage/info', () =>
        loggedIn ? HttpResponse.json({ email: 'ana@test.local', isEmailConfirmed: false }) : new HttpResponse(null, { status: 401 })),
      http.get('*/api/organizations', () => HttpResponse.json([])),
    ],
  }
}

async function fillAndSubmit(user: ReturnType<typeof renderApp>['user'], button: string) {
  await user.type(await screen.findByLabelText('E-mail'), 'ana@test.local')
  await user.type(screen.getByLabelText('Senha'), 'Passw0rd!')
  await user.click(screen.getByRole('button', { name: button }))
}

describe('login form states', () => {
  // C45
  it('login form states: disabled while pending, then invalid credentials keep the e-mail', async () => {
    const { gate, release } = deferred()
    server.use(anonymous(), http.post('*/api/auth/login', async () => {
      await gate
      return problem(401, 'Unauthorized')
    }))
    const { user } = renderApp('/login')

    await fillAndSubmit(user, 'Entrar')

    expect(await screen.findByRole('button', { name: 'Processando...' })).toBeDisabled()
    release()
    expect(await screen.findByRole('alert')).toHaveTextContent('E-mail ou senha inválidos.')
    expect(screen.getByLabelText('E-mail')).toHaveValue('ana@test.local')
  })

  // C45
  it('login form states: success goes to /organizations', async () => {
    const session = sessionAfterLogin()
    server.use(...session.handlers, http.post('*/api/auth/login', () => {
      session.markLoggedIn()
      return new HttpResponse(null, { status: 200 })
    }))
    const { user } = renderApp('/login')

    await fillAndSubmit(user, 'Entrar')

    expect(await screen.findByRole('heading', { name: 'Organizações' })).toBeInTheDocument()
    expect(screen.getByTestId('location')).toHaveTextContent('/organizations')
  })
})

describe('register form states', () => {
  // C46
  it('register form states: disabled while pending, then problem title keeps the e-mail', async () => {
    const { gate, release } = deferred()
    server.use(anonymous(), http.post('*/api/auth/register', async () => {
      await gate
      return problem(400, 'E-mail já cadastrado')
    }))
    const { user } = renderApp('/register')

    await fillAndSubmit(user, 'Criar conta')

    expect(await screen.findByRole('button', { name: 'Processando...' })).toBeDisabled()
    release()
    expect(await screen.findByRole('alert')).toHaveTextContent('E-mail já cadastrado')
    expect(screen.getByLabelText('E-mail')).toHaveValue('ana@test.local')
  })

  // C46
  it('register form states: success goes to /organizations', async () => {
    const session = sessionAfterLogin()
    server.use(
      ...session.handlers,
      http.post('*/api/auth/register', () => new HttpResponse(null, { status: 200 })),
      http.post('*/api/auth/login', () => {
        session.markLoggedIn()
        return new HttpResponse(null, { status: 200 })
      }),
    )
    const { user } = renderApp('/register')

    await fillAndSubmit(user, 'Criar conta')

    expect(await screen.findByRole('heading', { name: 'Organizações' })).toBeInTheDocument()
    expect(screen.getByTestId('location')).toHaveTextContent('/organizations')
  })
})
