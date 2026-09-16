import { render } from '@testing-library/react'
import { expect } from 'vitest'
import Icon from '../components/Icon'

describe('Icon', () => {
  it('renders an svg element with the requested icon', () => {
    const { container } = render(<Icon name="lock" />)
    const svg = container.querySelector('svg')

    expect(svg).not.toBeNull()
    expect(svg).toBeInTheDocument()
    expect(svg).toHaveAttribute('aria-hidden', 'true')
  })
})
