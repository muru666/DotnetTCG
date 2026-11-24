import { TestBed } from '@angular/core/testing';
import { ChoiceDialogComponent } from './choice-dialog';
import { PendingChoice } from '../../../../shared/models/dtos/game-state.model';

describe('ChoiceDialogComponent', () => {
  const pending: PendingChoice = {
    choiceId: 'attack:Electron Crush:optional-discard',
    playerId: 'u1',
    abilityName: '',
    promptLabel: '¿Descartar una Energía por +30?',
    options: [
      { optionId: 'yes', label: 'Sí', value: 'yes' },
      { optionId: 'no', label: 'No', value: 'no' },
    ],
  };

  it('Confirmar emite una sola vez ante doble clic (FE-014)', () => {
    TestBed.configureTestingModule({ imports: [ChoiceDialogComponent] });
    const fixture = TestBed.createComponent(ChoiceDialogComponent);
    fixture.componentRef.setInput('pendingChoice', pending);
    fixture.detectChanges();

    const option = fixture.nativeElement.querySelector('.choice-option') as HTMLElement;
    option.click();
    fixture.detectChanges();

    const emitted: unknown[] = [];
    fixture.componentInstance.resolve.subscribe(value => emitted.push(value));

    const confirm = fixture.nativeElement.querySelector('.choice-confirm') as HTMLElement;
    confirm.click();
    fixture.detectChanges();
    confirm.click(); // segundo clic: el botón queda deshabilitado tras emitir
    fixture.detectChanges();

    expect(emitted).toHaveLength(1);
    expect((confirm as HTMLButtonElement).disabled).toBe(true);
  });
});
