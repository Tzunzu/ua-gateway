# Decisions

## Accepted

1. Product Name
- Use UA Gateway as the product name
- Keep technical project names as UAGateway.*

2. UI Platform
- Use WinUI 3 for UAGateway.UI
- Keep UI Windows-only for now
- Keep UI isolated from service/core so another frontend can be built later

3. Runtime Platform
- Use .NET service architecture with a Windows Service host

4. OPC UA Stack
- Use OPC Foundation .NET stack package
- Current package: OPCFoundation.NetStandard.Opc.Ua (1.5.378.106)

5. Dev Workflow
- Maintain VS Code tasks for restore, build, test, and run
- Keep repository docs as the continuity source across machines

## Deferred

1. Service-to-UI contract transport
- Named pipes vs gRPC vs other local IPC

2. Namespace mapping model details
- Final mapping rules and transformation strategy

3. Certificate bootstrap UX
- How cert/trust onboarding should be exposed in UI
