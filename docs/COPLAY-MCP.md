# Coplay MCP integration

The repository targets Coplay's existing **MCP for Unity** as its Action Layer. This is not an additional MCP implementation.

## Existing Coplay tools used

- execute_menu_item — invokes the C# verification bridge.
- execute_code — optional direct call to the public verification API.
- read_console — optional targeted Console inspection; prefer bounded CLI log summaries for repeated historical errors.
- run_tests / get_test_job — Unity's existing asynchronous EditMode/PlayMode Test Runner tools.
- manage_scene, manage_gameobject, manage_components, manage_asset, manage_prefabs — existing authoring/editing tools. Do not replace them.

## Required flow

1. Use Coplay MCP to select the GameObjects relevant to the current task.
2. Invoke execute_menu_item(menu_path="Tools/AI Verification/Run Selected Checks").
3. Run unity-ai verify --suite logs,physics,ui from the Unity repository root.
4. For a Git checkpoint, first invoke execute_menu_item(menu_path="Tools/AI Verification/Save Scene and Assets"), then run unity-ai checkpoint create <name>.

## Why file-based reports

Coplay already provides the live Editor transport and executes commands on the Unity main thread. The C# verifier therefore only writes small JSON artifacts to .ai/artifacts/; the Node CLI reads those files. Do not add C# sockets, HTTP listeners, custom MCP tools or another Editor connection.

## Compatibility

The CLI is deliberately transport-independent. The C# bridge exposes public static methods and Unity menu items, so another Unity MCP could invoke the same operations without changing the verification engine.
