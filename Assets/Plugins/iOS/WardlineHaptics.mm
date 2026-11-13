// Native haptic feedback for Wardline (iOS). Called from C# via [DllImport("__Internal")].
#import <UIKit/UIKit.h>

extern "C" void _WardlineHaptic(int style)
{
    static UIImpactFeedbackGenerator *generators[3] = { nil, nil, nil };
    int idx = style < 0 ? 0 : (style > 2 ? 2 : style);
    if (generators[idx] == nil)
    {
        UIImpactFeedbackStyle s = idx == 0 ? UIImpactFeedbackStyleLight : (idx == 1 ? UIImpactFeedbackStyleMedium : UIImpactFeedbackStyleHeavy);
        generators[idx] = [[UIImpactFeedbackGenerator alloc] initWithStyle:s];
        [generators[idx] prepare];
    }
    [generators[idx] impactOccurred];
    [generators[idx] prepare];
}
