import { useEffect, useState, type FormEvent } from 'react'
import { api, type AnnouncementDto, type NotificationDto } from '../api/client'
import { useAuth } from '../auth/AuthContext'

const ADMIN = 'Administrator'
const MANAGER = 'PropertyManager'

export default function CommunicationPage() {
  const { user } = useAuth()
  const roles = user?.roles ?? []
  const isManager = roles.includes(ADMIN) || roles.includes(MANAGER)
  const isResident = roles.includes('Resident')

  const [notifications, setNotifications] = useState<NotificationDto[]>([])
  const [announcements, setAnnouncements] = useState<AnnouncementDto[]>([])
  const [error, setError] = useState('')

  const [propId, setPropId] = useState('')
  const [title, setTitle] = useState('')
  const [body, setBody] = useState('')
  const [properties, setProperties] = useState<{ id: string; name: string }[]>([])

  const load = async () => {
    try {
      setNotifications(await api.getNotifications())
      setAnnouncements(
        isResident ? await api.getMyAnnouncements() : await api.getAnnouncements(),
      )
    } catch (e) {
      setError((e as Error).message)
    }
  }

  useEffect(() => {
    ;(async () => {
      try {
        if (isManager) setProperties(await api.getProperties())
        await load()
      } catch (e) {
        setError((e as Error).message)
      }
    })()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const markRead = async (id: string) => {
    await api.markNotificationRead(id)
    await load()
  }

  const markAll = async () => {
    await api.markAllNotificationsRead()
    await load()
  }

  const publish = async (e: FormEvent) => {
    e.preventDefault()
    setError('')
    try {
      await api.publishAnnouncement({ propertyId: propId, title, body })
      setTitle('')
      setBody('')
      await load()
    } catch (err) {
      setError((err as Error).message)
    }
  }

  return (
    <div className="container">
      <h2>Communication</h2>
      {error && <div className="error">{error}</div>}

      <div className="row space-between">
        <h3>Notifications</h3>
        {notifications.some((n) => !n.isRead) && <button className="ghost" onClick={markAll}>Mark all read</button>}
      </div>
      {notifications.length === 0 && <p className="muted">No notifications.</p>}
      {notifications.map((n) => (
        <div key={n.id} className="panel" onClick={() => !n.isRead && markRead(n.id)} style={{ cursor: 'pointer' }}>
          <div className="row space-between">
            <strong>{n.title} {!n.isRead && <span className="pill pill--info">new</span>}</strong>
            <span className="muted">{new Date(n.createdAt).toLocaleString()}</span>
          </div>
          <p className={n.isRead ? 'muted' : ''}>{n.body}</p>
        </div>
      ))}

      <h3>Announcements</h3>
      {isManager && (
        <form className="panel" onSubmit={publish}>
          <h4>Publish announcement</h4>
          <label>
            Property
            <select value={propId} onChange={(e) => setPropId(e.target.value)} required>
              <option value="">Select property</option>
              {properties.map((p) => (
                <option key={p.id} value={p.id}>{p.name}</option>
              ))}
            </select>
          </label>
          <label>Title <input value={title} onChange={(e) => setTitle(e.target.value)} required /></label>
          <label>Body <textarea value={body} onChange={(e) => setBody(e.target.value)} required /></label>
          <button type="submit">Publish</button>
        </form>
      )}
      {announcements.length === 0 && <p className="muted">No announcements.</p>}
      {announcements.map((a) => (
        <div key={a.id} className="panel">
          <div className="row space-between">
            <strong>{a.title}</strong>
            <span className="muted">{a.propertyName}</span>
          </div>
          <p>{a.body}</p>
          <div className="muted">{new Date(a.publishedAt).toLocaleString()}</div>
        </div>
      ))}
    </div>
  )
}
