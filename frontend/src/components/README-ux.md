# UX building blocks (usage)

All user-scoped client state is stored under `pawzaroo:<namespace>:<userId>:<key>` (see `lib/safeStorage.ts`); every read/write is quota- and exception-safe.

## Pickers (`components/pickers`)
Replace raw UUID inputs. All share `PickerBaseProps`: `value` (id string), `onChange(id, item?)`, `aria-label`, `placeholder`, `clearable`.
```tsx
<PetPicker value={petId} onChange={setPetId} minAccess="Caretaker" aria-label="Pet" />
<UserPicker value={userId} onChange={setUserId} />          // GET /v1/users?q= (min 2 chars, 10 results)
<ProductPicker value={pid} onChange={setPid} storeId={sid} />
<StorePicker value={sid} onChange={setSid} status="Approved" />
```
Built on `components/ui/combobox.tsx` (Popover + cmdk: role=combobox, aria-activedescendant, arrow keys, loading/empty/error+retry).

## Drafts
```tsx
const draft = useDraft("my-form", values, { enabled: dirty, sanitize: stripSecrets });
<DraftBanner draft={draft} onRestore={(d) => setValues(d)} />
// on successful submit: draft.clear()
```
800ms debounce, 7 day TTL, versioned. Never put passwords in drafts.

## Query states
```tsx
<QueryBoundary query={q} empty={<EmptyState ... action={<Button/>} />} partial={q.data?.partial}>{(data) => ...}</QueryBoundary>
```
Handles skeleton, error+retry, offline (`useOnlineStatus`), 502/503/504 "unavailable", stale-data notice.

## Tables
`<DataTable caption="..." density="compact" | showDensityToggle columns=[{..., sortValue}] />` — `sortValue` makes a column sortable (`aria-sort`).

## Recents / search history / favorites / saved filters
- `useRecentItems().track({ id: "pet:123", kind: "pet", title, to })` (shown in the header `RecentItemsMenu`)
- `<SearchHistoryDropdown scope="store" value onChange onSubmit />`
- `<FavoriteButton kind="vet" id={id} title to />`, `useFavorite`
- `<SavedFiltersBar scope="admin-users" params={searchParams} onApply={setSearchParams} />`

## Undo and optimistic updates
```tsx
const undoable = useUndoableAction();
undoable({ key: `del:${id}`, message: t("x.deleted"), commit: () => api.delete(..), rollback: () => restore() });
```
UI changes immediately; the server call runs after 8s unless "Undo" is pressed; pending actions are flushed on route change/unmount/pagehide.
`useOptimisticMutation({ mutationFn, onMutate, onRollback, getKey })` adds rollback and an in-flight duplicate guard.

## Uploads
`FileUploadButton` (unchanged props; now compresses images, shows progress, click again to cancel) and `ImageUploader` (multi-file, crop via `cropAspect="square"|"16:9"`, retry, progress bars).

## i18n
New keys live in `lib/i18n.ux.ts` (merged in `i18n.ts`). API errors: `lib/apiErrors.ts` maps `error.code` -> `err.<code>`; add new backend codes to `API_ERROR_CODES` and both catalogs. Phone/units: `formatPhone`, `formatUnit`, `formatWeight` in `lib/format.ts`.
