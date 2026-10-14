---
name: angular-22-engineering
description: Build, review, and refactor Angular 22 applications using current Angular APIs, maintainable feature-first architecture, accessible UI, and secure API integration.
---

# Angular 22 Engineering Skill

## Mission

Produce Angular 22 code that is idiomatic, accessible, secure, performant, and easy to evolve. Prefer official Angular APIs and current documentation over habits from older Angular versions. Before changing code, inspect the repository and follow its established conventions unless they conflict with a rule here or the user asks otherwise.

> Project rule: this frontend ships **without automated tests** (no unit, integration, or
> end-to-end tests). Correctness is verified by strict typing, lint, successful builds,
> and manual review against the backend API contract. Do not add test files, test
> runners, or test scripts.

## 1. Mandatory workflow

1. Inspect `package.json`, lockfile, `angular.json`, `tsconfig*.json`, routes, HTTP setup, and existing feature structure before proposing changes.
2. Confirm the installed Angular major version. Do not silently upgrade dependencies or mix APIs from different majors.
3. Identify the user story, relevant feature, API contract, and loading/error/empty states.
4. Make the smallest coherent change. Avoid unrelated refactors and speculative abstractions.
5. Run the available checks: lint and type/build checks. Report commands run and anything not run.
6. If a command fails, distinguish failures introduced by the change from pre-existing failures; do not claim success without evidence.
7. When unsure about a version-specific API, check the official Angular v22 docs and release notes instead of guessing.

## 2. Core Angular 22 conventions

- Use standalone components, directives, and pipes. Standalone is the default; do not add `standalone: true` unless the repository/tooling specifically requires it.
- Use the native template control flow: `@if`, `@else`, `@for`, and `@switch`; do not introduce `*ngIf`, `*ngFor`, or `*ngSwitch` in new code.
- Use signals for local synchronous UI state; `computed()` for derived state; `effect()` only for synchronizing with an external side effect, not for ordinary state derivation.
- Prefer `input()`, `output()`, and `model()` APIs for new components where appropriate. Use the existing convention in a legacy area if migration is outside scope.
- Prefer `inject()` for dependency injection, consistent with Angular's current style guide.
- Angular 22 defaults to `OnPush` change detection; do not explicitly set `ChangeDetectionStrategy.OnPush` unless there is a clear compatibility or documentation reason.
- Use the current Angular 22 service conventions supported by the project. Prefer the documented singleton service decorator/API if the installed version and project use it; otherwise use `@Injectable({ providedIn: 'root' })`. Never guess an API name—verify it first.
- Prefer `class` and `style` bindings instead of `NgClass` and `NgStyle` for new code.
- Import only the directives, pipes, and APIs actually used. Avoid importing `CommonModule` just as a blanket default.
- Prefer one primary concept per file. Keep related component template and styles next to the component.
- Use kebab-case file names and organize by feature/domain, not giant global folders named only `components`, `services`, or `models`.
- Use `protected` for members used only by a component template where it improves encapsulation and matches project style.

## 3. TypeScript standards

- Keep TypeScript strict mode enabled. Preserve strict null checks and typed forms.
- Avoid `any`. Use concrete types or `unknown` plus narrowing when data is not trusted.
- Infer obvious types; explicitly annotate public contracts, API boundaries, complex return types, and domain-relevant values.
- Prefer discriminated unions for state such as `idle | loading | success | error` when that makes invalid states harder to represent.
- Use `readonly` where it communicates an invariant; do not add ceremony without value.
- Avoid non-null assertions (`!`) and unsafe casts unless the invariant is demonstrated and documented.
- Keep pure transformations pure. Do not mutate signal values or shared state in place; use `set()` or `update()`.
- Use `enum` only when the project's conventions or interoperability justify it; string-literal unions are often simpler for UI/API states.
- Avoid utility types and generic abstractions that obscure rather clarify the business concept.

## 4. Architecture for feature-first Angular

Organize the front-end by business capability. Example (adapt names to the actual product):

```text
src/app/
  app.config.ts
  app.routes.ts
  core/
    auth/
    http/
    layout/
    error-handling/
  shared/
    ui/
    pipes/
    directives/
  features/
    orders/
      pages/
      ui/
      data-access/
      models/
      orders.routes.ts
    products/
    reports/
```

Guidelines:

- `core/`: application-wide infrastructure with one app-level instance (authentication integration, interceptors, global error handling, shell/layout where appropriate).
- `shared/`: genuinely reusable, domain-agnostic UI and utilities. Do not place business rules here merely because two files use them.
- `features/<feature>/`: screens, feature UI, feature-specific state, API access, and types. Keep related code close together.
- `pages/`: route-level composition and orchestration. Pages should coordinate view state, not implement business rules.
- `ui/`: presentational components that render inputs and emit outputs. Keep them independent of HTTP where practical.
- `data-access/`: API clients, DTOs, mapping, and feature-specific repository/facade logic. Do not make every tiny method a separate service automatically.
- Avoid circular dependencies between features. A feature should not reach into another feature's private files; extract a deliberate shared contract or compose at a higher level.
- Avoid creating a frontend mirror of every .NET project/layer. Organize Angular around user-facing capabilities and UI responsibilities, not backend namespace parity.
- Start with a modular monolith front-end. Do not introduce micro-frontends, global state libraries, or elaborate abstractions without concrete scale/team requirements.

## 5. Clean Architecture boundary with .NET

The Angular application is a client of the backend API, not another implementation of backend Clean Architecture.

- Angular handles presentation, navigation, user input, client-side validation for usability, and client UI state.
- .NET remains authoritative for business invariants, authorization, persistence, transactions, pricing, and validation that must be trusted.
- Treat HTTP DTOs as transport contracts. Do not assume backend domain entities should be copied directly into Angular types.
- Keep API DTOs separate from UI view models when the shape or meaning differs. Add explicit mapping functions at the boundary when that improves clarity.
- Use a typed API client per feature or bounded capability. Centralize base URL/configuration and cross-cutting HTTP concerns.
- Components should not build URLs, parse raw response envelopes, or duplicate authentication/error-handling logic.
- Avoid business logic in interceptors. Interceptors are for cross-cutting transport concerns such as correlation IDs, auth headers where appropriate, and consistent transport instrumentation.
- The API must enforce authorization independently of route guards or hidden UI controls.
- Do not expose secrets or private API credentials in the Angular bundle. Anything shipped to the browser is public.
- Define and document API error contracts with the .NET backend. Translate transport errors into useful UI messages without leaking stack traces or internal details.

## 6. State and reactivity

Choose the simplest state tool that fits the lifetime and source of the state:

- Component-local state: `signal()`.
- Derived local state: `computed()`; keep derivations pure.
- State synchronized with a changing selection or other reactive source: consider `linkedSignal()` when its semantics match.
- HTTP streams, events, and cancellation-oriented async composition: RxJS remains appropriate. Use operators intentionally and unsubscribe safely (e.g. `takeUntilDestroyed()` when needed).
- Convert an Observable to a signal with `toSignal()` when the template or signal-based logic benefits from it; create it once in an injection context and understand its initial-value/error behavior.
- Do not convert every Observable to a signal by default. Avoid repeated subscriptions or repeated `toSignal()` calls for the same stream.
- Use `toObservable()` when a signal needs to feed an RxJS pipeline.
- Avoid `effect()` to copy one signal into another or calculate derived values; use `computed()`/`linkedSignal()` instead.
- Do not put server state into a global store by default. Start with feature-scoped state/facades and add a state library only when shared state complexity, debugging needs, or team conventions justify it.
- Model loading, empty, success, and failure states explicitly. Avoid booleans that can form contradictory combinations when a discriminated union would be clearer.
- For search/typeahead, debounce where useful, cancel stale requests with appropriate RxJS operators, and handle empty queries explicitly.

Example:

```ts
import { computed, signal } from '@angular/core';

type CartLine = { productId: string; quantity: number; unitPrice: number };

export class CartState {
  private readonly linesState = signal<readonly CartLine[]>([]);
  readonly lines = this.linesState.asReadonly();
  readonly itemCount = computed(() =>
    this.linesState().reduce((total, line) => total + line.quantity, 0),
  );
  readonly total = computed(() =>
    this.linesState().reduce((sum, line) => sum + line.quantity * line.unitPrice, 0),
  );

  add(line: CartLine): void {
    this.linesState.update(lines => [...lines, line]);
  }
}
```

This is UI state, not an authority for server-side pricing or order totals.

## 7. Components and templates

- Keep components focused on one UI responsibility; move reusable calculations to pure functions or focused services.
- Prefer small, clear templates. Avoid nested ternaries, complex transformations, hidden side effects, and business rules in templates.
- Use `@for (item of items(); track item.id)` with a stable identity key. Avoid `$index` when the collection can reorder or items can be inserted/removed.
- Use `@empty` where useful for empty collections.
- Use `@defer` for genuinely heavy or below-the-fold UI after checking its triggers and fallback behavior; do not apply it indiscriminately.
- Use `NgOptimizedImage` for ordinary static image assets where appropriate; provide meaningful `alt` text and dimensions/aspect ratio to prevent layout shifts.
- Prefer native HTML elements with correct semantics. Use buttons for actions and links for navigation.
- Define component inputs/outputs as a clear public API. Avoid components reaching into parent internals or injecting arbitrary parent components.
- Keep styling local by default. Avoid global CSS selectors that leak into feature components.
- Avoid direct DOM manipulation and `ElementRef` unless there is a clear need such as focus management; prefer Angular bindings and host metadata.
- Put host listeners and host bindings in the component/directive `host` metadata rather than introducing `@HostListener` or `@HostBinding` in new code.

## 8. Forms and validation

- For new Angular 22 forms, evaluate stable Signal Forms first when they fit the project's maturity and requirements. Verify the exact current API in official v22 documentation before using it.
- When Signal Forms are not appropriate, prefer Reactive Forms with typed controls over template-driven forms for non-trivial forms.
- Keep validation rules close to the form/domain contract and reuse only when it prevents real duplication.
- Client-side validation improves UX; backend validation remains mandatory.
- Show accessible labels, field-level errors, and an appropriate summary for complex forms. Associate errors with controls and announce important validation changes accessibly.
- Do not erase user input on transient API failures. Prevent duplicate submissions while a request is pending, but keep the UI recoverable.
- Normalize and validate values at the boundary; do not silently coerce malformed API data.

## 9. Routing and loading

- Use `provideRouter()` and route configuration consistent with the application setup.
- Lazy-load feature routes with `loadChildren` or standalone route components with `loadComponent` where appropriate.
- Use route guards for navigation/UX, not as the actual security boundary.
- Use resolvers only when pre-navigation loading materially improves the UX or route invariants require it; avoid blocking navigation for every API call.
- Provide not-found and error routes, meaningful page titles, and predictable navigation behavior.
- Use route parameters for shareable navigational state (e.g. selected record, filters when appropriate); avoid duplicating URL state unnecessarily in global stores.

## 10. HTTP, authentication, and errors

- Configure `provideHttpClient()` centrally. Use functional interceptors when suitable and register them explicitly.
- Keep API base URL in environment/runtime configuration; never hardcode deployment-specific URLs across services.
- Use typed request/response models, but remember TypeScript types do not validate JSON at runtime. Validate untrusted external data where risk warrants it.
- Handle expected failures at the feature boundary and unexpected failures centrally. Preserve enough context for diagnostics without showing sensitive internals to users.
- Do not swallow errors into empty arrays or success-shaped defaults unless that is an explicit product decision.
- Avoid automatic retries for non-idempotent mutations unless an idempotency strategy makes them safe.
- For cookie-based authentication, configure credential and CSRF protections consistently with the backend. For token-based authentication, do not assume localStorage is safe by default; choose storage and refresh flows based on the threat model.
- Never log passwords, access tokens, session cookies, personal data, or full sensitive payloads.
- Handle CORS on the server/API configuration; it is not fixed by Angular code.

## 11. Security

- Rely on Angular's normal template escaping and sanitization; never bypass sanitization for convenience.
- Avoid `bypassSecurityTrustHtml`, `bypassSecurityTrustUrl`, and direct unsafe DOM writes unless a reviewed, narrowly scoped security requirement proves them necessary.
- Never render untrusted strings as templates or compile user-provided templates.
- Treat all browser input, query parameters, local storage, and API data as untrusted.
- Do not rely on client-side role checks for authorization. The API must authorize each protected operation.
- Use a restrictive Content Security Policy and Trusted Types where compatible with the deployment; coordinate headers with the hosting/API team.
- Keep dependencies updated and review packages for maintenance, provenance, and known vulnerabilities.

## 12. Accessibility and UX

- Aim for WCAG 2.2 AA, including keyboard operation, visible focus, sufficient contrast, accessible names, and meaningful semantics.
- Verify keyboard operation and screen-reader semantics by manual walkthrough; do not build custom comboboxes/dialogs without accounting for keyboard and screen-reader behavior.
- Every form control needs a programmatic label. Images need meaningful alt text or empty alt for decoration.
- Dialogs should manage focus, support Escape when appropriate, and return focus to the trigger.
- Do not convey errors/status using color alone. Respect reduced-motion preferences.
- Every async screen needs clear loading, empty, error, and success feedback; avoid indefinite spinners and dead-end errors.

## 13. Performance

- Start with Angular defaults; measure before optimizing.
- Lazy-load feature routes. Use `@defer` for large optional UI and split large dependencies when evidence supports it.
- Keep template expressions cheap and avoid creating new objects/functions repeatedly in hot rendering paths without need.
- Track repeated lists by stable IDs. Prefer signals and immutable updates that make dependencies clear.
- Avoid broad global state and unnecessary subscriptions.
- Use `NgOptimizedImage` for image optimization where applicable and avoid layout shifts.
- Consider SSR/prerendering only if SEO, first-load performance, or public content requires it; an authenticated business dashboard often does not need SSR by default.
- Use Angular DevTools and browser performance tools to identify actual bottlenecks before adding memoization or complexity.

## 14. Naming and code organization

- Use descriptive names that express intent: `OrdersApi`, `OrderListPage`, `OrderStatusBadge`, `loadOrders`, `isSubmitting`.
- Avoid generic names such as `Helper`, `Manager`, `CommonService`, `BaseComponent`, and `Utils` when a more specific concept exists.
- Use singular nouns for one entity and plural names for collections/features consistently.
- Keep domain terms consistent with API and product language; do not introduce synonyms for the same concept.
- Avoid barrel files unless they simplify a real public API and do not create circular imports or obscure dependencies.
- Do not create abstractions for hypothetical future needs. Refactor when a repeated pattern is real and stable.

## 15. Definition of done

Before declaring a task complete, verify:

- [ ] The implementation matches the requested behavior and existing architecture.
- [ ] Types are strict; no avoidable `any`, unsafe casts, or non-null assertions.
- [ ] Loading, empty, error, and success states are handled where relevant.
- [ ] API errors and security boundaries are respected.
- [ ] Keyboard navigation, labels, focus, and semantics are considered.
- [ ] Lint and type/build checks were run.
- [ ] No unrelated dependencies, generated files, or broad refactors were introduced.
- [ ] The final report lists changed areas, key decisions, checks run, and known limitations.

## 16. Anti-patterns to reject

- Putting business rules, authorization decisions, or authoritative calculations in Angular.
- A single giant component/service or one global state store for everything.
- Organizing every feature's files into global type-only folders.
- Using `effect()` as a substitute for `computed()` or for routine state synchronization.
- Using `any`, blind casts, or `!` to silence type errors instead of fixing the model.
- Calling HTTP directly from many components or duplicating error/auth logic.
- Treating route guards as security.
- Swallowing errors, retrying unsafe mutations automatically, or logging secrets.
- Optimizing without measurement or adding libraries for trivial needs.
- Introducing Angular APIs from a newer version without verifying compatibility with Angular 22.

## 17. Official references

Use these as the source of truth and check the v22 documentation when an API is version-sensitive:

- Angular v22 release: https://angular.dev/events/v22
- Angular style guide: https://angular.dev/style-guide
- Angular security: https://angular.dev/best-practices/security
- Angular performance: https://angular.dev/best-practices/performance
- Angular signals: https://angular.dev/guide/signals
- Angular forms: https://angular.dev/guide/forms
- Angular routing: https://angular.dev/guide/routing
