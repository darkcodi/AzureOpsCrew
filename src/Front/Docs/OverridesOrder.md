# Blazor Component Method Overrides in Order of Execution

## First render

For a normal component instance, the lifecycle methods run in this order:

```text
SetParametersAsync(ParameterView parameters)
OnInitialized()
OnInitializedAsync()
OnParametersSet()
OnParametersSetAsync()
Render
OnAfterRender(bool firstRender)
OnAfterRenderAsync(bool firstRender)
```

## Subsequent renders

When the parent rerenders and supplies parameters again, the usual order is:

```text
SetParametersAsync(ParameterView parameters)
OnParametersSet()
OnParametersSetAsync()
Render
OnAfterRender(bool firstRender: false)
OnAfterRenderAsync(bool firstRender: false)
```

## Notes

* `OnInitialized` and `OnInitializedAsync` run once per component instance.
* `OnParametersSet` and `OnParametersSetAsync` run whenever the component receives parameters from its parent.
* `OnAfterRender` and `OnAfterRenderAsync` run after rendering is complete.
* `OnAfterRenderAsync` runs after `OnAfterRender`.
* `ShouldRender()` can prevent a rerender if it returns `false`.
* `SetParametersAsync(...)` is the lowest-level parameter hook and is usually overridden only when needed.

## Quick mental model

* **`OnInitialized*`** = one-time setup
* **`OnParametersSet*`** = react to parameter changes
* **`OnAfterRender*`** = run logic after the UI is rendered

## Cheat-sheet

### First render

```text
SetParametersAsync
→ OnInitialized
→ OnInitializedAsync
→ OnParametersSet
→ OnParametersSetAsync
→ Render
→ OnAfterRender
→ OnAfterRenderAsync
```

### Later renders

```text
SetParametersAsync
→ OnParametersSet
→ OnParametersSetAsync
→ Render
→ OnAfterRender
→ OnAfterRenderAsync
```
