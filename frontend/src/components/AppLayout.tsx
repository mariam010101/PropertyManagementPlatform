import { useEffect, useRef, useState } from 'react'
import { Link, NavLink, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import Icon from './Icon'
import { navigationFor, routeFor } from '../navigation'

const initialsOf = (firstName?: string, lastName?: string, email?: string) => {
  const letters = `${firstName?.[0] ?? ''}${lastName?.[0] ?? ''}`.trim()
  return (letters || email?.[0] || '?').toUpperCase()
}

/**
 * The application shell: a persistent sidebar on desktop, a collapsible drawer on
 * mobile, a top bar carrying the current section and the signed-in user's menu.
 * Authentication pages render outside this shell.
 */
export default function AppLayout() {
  const { user, logout } = useAuth()
  const location = useLocation()
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [menuOpen, setMenuOpen] = useState(false)
  const menuRef = useRef<HTMLDivElement>(null)

  const roles = user?.roles ?? []
  const items = navigationFor(roles)
  const current = routeFor(location.pathname)
  const title = location.pathname === '/forbidden' ? 'Not authorised' : (current?.label ?? 'PMP')
  const displayName = [user?.firstName, user?.lastName].filter(Boolean).join(' ') || user?.email || 'Signed in'

  useEffect(() => {
    if (!menuOpen) return

    const onPointerDown = (event: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(event.target as Node)) setMenuOpen(false)
    }
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setMenuOpen(false)
        setDrawerOpen(false)
      }
    }

    document.addEventListener('mousedown', onPointerDown)
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('mousedown', onPointerDown)
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [menuOpen])

  return (
    <div className="shell">
      <a className="skip-link" href="#main">
        Skip to content
      </a>

      <aside id="app-nav" className={`shell__sidebar${drawerOpen ? ' shell__sidebar--open' : ''}`}>
        <div className="shell__brand">
          <span className="shell__brand-mark">PMP</span>
          <span className="shell__brand-sub">Property Management</span>
        </div>

        <nav className="nav" aria-label="Main">
          {items.map((item) => (
            <NavLink
              key={item.path}
              to={item.path}
              end={item.index}
              className={({ isActive }) => `nav__item${isActive ? ' nav__item--active' : ''}`}
              onClick={() => {
                setDrawerOpen(false)
                setMenuOpen(false)
              }}
            >
              <Icon name={item.icon} />
              <span>{item.label}</span>
            </NavLink>
          ))}
        </nav>
      </aside>

      {drawerOpen && <div className="shell__scrim" onClick={() => setDrawerOpen(false)} aria-hidden="true" />}

      <div className="shell__body">
        <header className="shell__topbar">
          <button
            type="button"
            className="icon-button shell__menu"
            aria-label={drawerOpen ? 'Close navigation' : 'Open navigation'}
            aria-expanded={drawerOpen}
            aria-controls="app-nav"
            onClick={() => setDrawerOpen((open) => !open)}
          >
            <Icon name={drawerOpen ? 'close' : 'menu'} />
          </button>

          <span className="shell__title">{title}</span>

          <div className="user-menu" ref={menuRef}>
            <button
              type="button"
              className="user-menu__button"
              aria-haspopup="menu"
              aria-expanded={menuOpen}
              onClick={() => setMenuOpen((open) => !open)}
            >
              <span className="avatar" aria-hidden="true">
                {initialsOf(user?.firstName, user?.lastName, user?.email)}
              </span>
              <span className="user-menu__name">{displayName}</span>
              <Icon name="chevronDown" size={16} />
            </button>

            {menuOpen && (
              <div className="user-menu__panel" role="menu" aria-label="Account">
                <div className="user-menu__head">
                  <strong>{displayName}</strong>
                  <span className="muted">{user?.email}</span>
                  <span className="user-menu__roles">{roles.join(' · ')}</span>
                </div>
                <Link
                  role="menuitem"
                  className="user-menu__action"
                  to="/account/password"
                  onClick={() => setMenuOpen(false)}
                >
                  <Icon name="lock" size={16} />
                  Change password
                </Link>
                <button
                  type="button"
                  role="menuitem"
                  className="user-menu__action"
                  onClick={() => void logout()}
                >
                  <Icon name="logout" size={16} />
                  Sign out
                </button>
              </div>
            )}
          </div>
        </header>

        <main id="main" className="shell__main">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
