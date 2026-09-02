import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** Avatar circular com iniciais (df-avatar fallback). */
@Component({
  selector: 'nx-avatar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'nx-avatar',
    '[style.width.px]': 'size()',
    '[style.height.px]': 'size()',
    '[style.flexBasis.px]': 'size()',
    role: 'img',
    '[attr.aria-label]': 'name()',
  },
  template: `{{ initials() }}`,
})
export class NxAvatar {
  readonly name = input('');
  readonly size = input(34);
  readonly initials = computed(() => {
    const label = this.name().includes('@') ? this.name().split('@')[0] : this.name();
    const parts = label.trim().split(/[.\s_-]+/).filter(Boolean);
    return ((parts[0]?.[0] ?? '?') + (parts[1]?.[0] ?? '')).toUpperCase();
  });
}
