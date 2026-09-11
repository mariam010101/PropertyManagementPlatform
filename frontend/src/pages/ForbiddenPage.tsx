import { Link } from 'react-router-dom'
import Icon from '../components/Icon'

/**
 * Shown inside the shell when a signed-in user reaches a route their roles do not
 * include, instead of silently redirecting them without explanation.
 */
export default function ForbiddenPage() {
  return (
    <div className="container">
      <div className="forbidden">
        <Icon name="lock" size={28} />
        <h2>You don’t have access to this page</h2>
        <p className="muted">
          Your account’s roles don’t include this area. If you believe this is wrong, ask an administrator to
          review your roles.
        </p>
        <Link className="btn btn--primary" to="/">
          Back to dashboard
        </Link>
      </div>
    </div>
  )
}
