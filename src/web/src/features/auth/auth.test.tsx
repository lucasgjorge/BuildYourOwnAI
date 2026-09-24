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
      http.get('*/api/auth/me', () =>
        loggedIn ? HttpResponse.json({ email: 'ana@test.local', fullName: 'Ana Souza' }) : new HttpResponse(null, { status: 401 })),
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

async function fillRegisterAndSubmit(user: ReturnType<typeof renderApp>['user']) {
  await user.type(await screen.findByLabelText('Nome completo'), 'Maria da Silva')
  await fillAndSubmit(user, 'Criar conta')
}

describe('register form states', () => {
  // C46, user-name C17
  it('register form states: disabled while pending, then problem title keeps the name and e-mail', async () => {
    const { gate, release } = deferred()
    server.use(anonymous(), http.post('*/api/auth/register', async () => {
      await gate
      return problem(400, 'E-mail já cadastrado')
    }))
    const { user } = renderApp('/register')

    await fillRegisterAndSubmit(user)

    expect(await screen.findByRole('button', { name: 'Processando...' })).toBeDisabled()
    release()
    expect(await screen.findByRole('alert')).toHaveTextContent('E-mail já cadastrado')
    expect(screen.getByLabelText('Nome completo')).toHaveValue('Maria da Silva')
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

    await fillRegisterAndSubmit(user)

    expect(await screen.findByRole('heading', { name: 'Organizações' })).toBeInTheDocument()
    expect(screen.getByTestId('location')).toHaveTextContent('/organizations')
  })
})

describe('full name', () => {
  // user-name C15
  it('register form asks the full name first', async () => {
    server.use(anonymous())
    renderApp('/register')

    const name = await screen.findByLabelText('Nome completo')
    expect(name).toBeRequired()
    expect(name).toHaveAttribute('maxLength', '100')
    const fields = screen.getAllByRole('textbox')
    expect(fields[0]).toBe(name)
    expect(fields[1]).toBe(screen.getByLabelText('E-mail'))
  })

  // user-name C16
  it('register sends the full name', async () => {
    const bodies: unknown[] = []
    server.use(anonymous(), http.post('*/api/auth/register', async ({ request }) => {
      bodies.push(await request.json())
      return problem(400, 'E-mail já cadastrado')
    }))
    const { user } = renderApp('/register')

    await fillRegisterAndSubmit(user)

    await waitFor(() => expect(bodies).toEqual([{ fullName: 'Maria da Silva', email: 'ana@test.local', password: 'Passw0rd!' }]))
  })

  // user-name C20
  it('login form has no full name', async () => {
    server.use(anonymous())
    renderApp('/login')

    expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument()
    expect(screen.queryByLabelText('Nome completo')).not.toBeInTheDocument()
  })

  // user-name C18
  it.each(['/organizations', '/gaps'])('top bar shows the logged user name (%s)', async path => {
    server.use(http.get('*/api/auth/me', () => HttpResponse.json({ email: 'ana@test.local', fullName: 'Ana Souza' })))
    renderApp(path)

    expect(await screen.findByRole('banner', { name: 'Usuário logado' })).toHaveTextContent('Ana Souza')
  })

  // user-name C19
  it('top bar falls back to the e-mail', async () => {
    server.use(http.get('*/api/auth/me', () => HttpResponse.json({ email: 'ana@test.local', fullName: null })))
    renderApp('/organizations')

    expect(await screen.findByRole('banner', { name: 'Usuário logado' })).toHaveTextContent('ana@test.local')
  })
})
