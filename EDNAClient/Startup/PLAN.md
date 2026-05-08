# Startup State Machine -- Future Stages

## States
- NoEmpyrion: Empyrion not found via Steam registry. Show advisory message.
- NoEsb: ESB mod not installed. Offer to run EsbInstaller (see Setup/PLAN.md).
- NoMqtt: ESB_Info.yaml found but broker unreachable. Offer to run MosquittoInstaller.
- Ready: Full connect, normal operation.

## Future work
- Connect state detection to TrayIconManager icon variants (gray/amber/green)
- Drive Setup wizards from tray context menu based on current state
- Re-check state periodically so tray self-heals when ESB or broker comes online
