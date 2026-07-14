# VSA screenshots

Drop analyzer screen captures here (referenced from the tutorials and the Manual Verification doc).

Capture them over VISA with the harness:

```powershell
ESG-SignalCreator.HilHarness.exe --capture-screen docs/images/vsa/<name>.png --vsa <resource> --vsa-model <e4406a|n9010a>
```

Suggested names: `cw-result.png`, `am-result.png`, `fm-result.png`, `iq-multitone-result.png`.
Images are PNG on the N9010A (X-Series) and GIF on the E4406A.
