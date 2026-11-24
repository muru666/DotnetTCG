import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { Subject, of } from 'rxjs';
import { vi } from 'vitest';
import { GameSearcherComponent } from './lobby-page';
import { GameService } from '../../../../shared/services/api/game.service';
import { BackgroundImageService } from '../../../../core/services/background-image.service';

describe('GameSearcherComponent filtros', () => {
  it('selectFilter cancela la petición anterior de lista (FE-016)', () => {
    // Cada listGames devuelve un Subject nuevo: al cambiar de filtro, la
    // suscripción previa debe cancelarse (antes la respuesta vieja pisaba a la
    // nueva y quedaba la lista desfasada respecto al filtro visible).
    const subjects: Subject<never>[] = [];
    const gameService = {
      getMyActiveGame: () => of(null),
      listMyResumableGames: () => of([]),
      listGames: vi.fn(() => {
        const subject = new Subject<never>();
        subjects.push(subject);
        return subject.asObservable();
      }),
    };
    TestBed.configureTestingModule({
      providers: [
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: GameService, useValue: gameService },
        { provide: BackgroundImageService, useValue: { getAuthBackgroundImage: () => '' } },
      ],
    });

    const component = TestBed.runInInjectionContext(() => new GameSearcherComponent());
    component.ngOnInit();
    expect(subjects).toHaveLength(1);

    const internals = component as unknown as {
      selectFilter(filter: string): void;
    };
    internals.selectFilter('privadas');

    expect(subjects).toHaveLength(2);
    expect(subjects[0].observed).toBe(false); // cancelada
    expect(subjects[1].observed).toBe(true);  // la nueva sigue viva

    component.ngOnDestroy();
  });
});
