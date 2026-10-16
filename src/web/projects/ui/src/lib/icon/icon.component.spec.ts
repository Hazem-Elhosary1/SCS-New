import { Component, input } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { DRAWN_BEFORE_095 } from './icon-drawn.fixture';
import { SCS_ICONS, ScsIconComponent, ScsIconName } from './icon.component';

@Component({
  imports: [ScsIconComponent],
  template: `<div [attr.dir]="dir()"><scs-icon [name]="name()" [size]="size()" /></div>`,
})
class Host {
  readonly name = input<ScsIconName>('mail');
  readonly size = input(20);
  readonly dir = input<'rtl' | 'ltr'>('rtl');
}

function draw(name: ScsIconName, dir: 'rtl' | 'ltr' = 'rtl', size = 20) {
  const fixture = TestBed.createComponent(Host);
  fixture.componentRef.setInput('name', name);
  fixture.componentRef.setInput('dir', dir);
  fixture.componentRef.setInput('size', size);
  fixture.detectChanges();
  const host = fixture.nativeElement.querySelector('scs-icon') as HTMLElement;
  return { host, svg: host.querySelector('svg') as SVGSVGElement };
}

/** Each shape of an icon as "tag attr=value …", attributes sorted, for comparing drawings. */
function shapesOf(svg: Element): string[] {
  return [...svg.children].map((shape) => {
    const attributes = [...shape.attributes]
      .filter((attribute) => !attribute.name.startsWith('_ng') && !['fill', 'stroke'].includes(attribute.name))
      .map((attribute) => `${attribute.name}=${Number.isNaN(Number(attribute.value)) ? attribute.value.replace(/\s+/g, ' ') : Number(attribute.value)}`)
      .sort();
    return [shape.tagName.toLowerCase(), ...attributes].join(' ');
  });
}

/** The 53 icons as the template drew them before spec 095. */
function drawnBefore(): Map<string, string[]> {
  const drawings = new Map<string, string[]>();
  for (const [, name, inner] of DRAWN_BEFORE_095.matchAll(/@case \('([a-z-]+)'\) \{([\s\S]*?)\n {8}\}/g)) {
    const svg = new DOMParser().parseFromString(`<svg xmlns="http://www.w3.org/2000/svg">${inner}</svg>`, 'image/svg+xml');
    drawings.set(name, shapesOf(svg.documentElement));
  }
  return drawings;
}

describe('scs-icon, drawn from the registry (spec 095)', () => {
  it('draws the 53 icons drawn by hand exactly as before', () => {
    const before = drawnBefore();
    expect(before.size).toBe(53);
    for (const [name, shapes] of before) {
      const { svg } = draw(name as ScsIconName);
      expect(shapesOf(svg), name).toEqual(shapes);
      expect(svg.getAttribute('viewBox'), name).toBe('0 0 24 24');
      expect(svg.getAttribute('stroke-width'), name).toBe('1.75');
    }
  });

  it('has every registry entry drawn, with its size, and hidden from screen readers', () => {
    for (const name of Object.keys(SCS_ICONS) as ScsIconName[]) {
      const { host, svg } = draw(name, 'rtl', 16);
      expect(svg.children.length, name).toBe(SCS_ICONS[name].shapes.length);
      expect(svg.getAttribute('width')).toBe('16');
      expect(host.getAttribute('aria-hidden')).toBe('true');
    }
  });

  it('marks only the icons that point, with the direction they are drawn for', () => {
    expect(draw('back').host.getAttribute('data-points')).toBe('rtl');
    expect(draw('chevron-start').host.getAttribute('data-points')).toBe('ltr');
    expect(draw('sign-out').host.getAttribute('data-points')).toBe('ltr');
    expect(draw('bell').host.getAttribute('data-points')).toBeNull();
    const pointing = (Object.keys(SCS_ICONS) as ScsIconName[]).filter((name) => 'points' in SCS_ICONS[name]);
    expect(pointing.sort()).toEqual(['back', 'chevron-start', 'menu-refund', 'menu-send', 'send', 'sign-out']);
  });

  it('paints a filled shape with the colour of the text and leaves a stroked one unfilled', () => {
    const { svg } = draw('mail');
    expect(svg.getAttribute('fill')).toBe('none');
    expect(svg.getAttribute('stroke')).toBe('currentColor');
    for (const shape of svg.children) {
      expect(shape.getAttribute('fill')).toBeNull();
    }
  });
});
