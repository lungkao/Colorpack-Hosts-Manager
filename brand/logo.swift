// วาดโลโก้ COLORPACK CREATIONS เป็น PNG จากพิกัดเดียวกับ colorpack-logo.svg (viewBox 680×172)
// Renders the COLORPACK CREATIONS wordmark to PNG using the same geometry as the SVG.
// ใช้ / usage: swiftc logo.swift -o logo && ./logo <fonts-dir> <out-dir>
import AppKit
import CoreText

let args = CommandLine.arguments
let fontDir = args.count > 1 ? args[1] : "fonts"
let outDir = args.count > 2 ? args[2] : "."

for f in ["Kanit-Bold.ttf", "Kanit-Light.ttf"] {
    CTFontManagerRegisterFontsForURL(URL(fileURLWithPath: fontDir + "/" + f) as CFURL, .process, nil)
}

func color(_ hex: UInt32, _ a: CGFloat = 1) -> CGColor {
    CGColor(red: CGFloat((hex >> 16) & 255) / 255, green: CGFloat((hex >> 8) & 255) / 255,
            blue: CGFloat(hex & 255) / 255, alpha: a)
}

let W: CGFloat = 680, H: CGFloat = 172, S: CGFloat = 4   // scale ×4 → 2720×688

/// วาดข้อความให้กว้างพอดี target (เหมือน textLength ใน SVG)
func draw(_ ctx: CGContext, _ text: String, font: String, size: CGFloat, x: CGFloat, baseline: CGFloat,
          target: CGFloat, stretchGlyphs: Bool, fill: CGColor) {
    let f = CTFontCreateWithName(font as CFString, size * S, nil)
    func line(kern: CGFloat) -> CTLine {
        let attrs: [NSAttributedString.Key: Any] = [
            NSAttributedString.Key(kCTFontAttributeName as String): f,
            NSAttributedString.Key(kCTForegroundColorAttributeName as String): fill,
            NSAttributedString.Key(kCTKernAttributeName as String): kern
        ]
        return CTLineCreateWithAttributedString(NSAttributedString(string: text, attributes: attrs))
    }
    var l = line(kern: 0)
    let natural = CGFloat(CTLineGetTypographicBounds(l, nil, nil, nil))
    var sx: CGFloat = 1
    if stretchGlyphs {
        sx = target * S / natural
    } else {
        let kern = (target * S - natural) / CGFloat(max(text.count - 1, 1))
        l = line(kern: kern)
    }
    ctx.saveGState()
    ctx.textMatrix = CGAffineTransform(scaleX: sx, y: 1)
    ctx.textPosition = CGPoint(x: x * S, y: (H - baseline) * S)
    CTLineDraw(l, ctx)
    ctx.restoreGState()
}

func render(pack: CGColor, name: String) {
    let w = Int(W * S), h = Int(H * S)
    let ctx = CGContext(data: nil, width: w, height: h, bitsPerComponent: 8, bytesPerRow: 0,
                        space: CGColorSpaceCreateDeviceRGB(),
                        bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    ctx.setShouldAntialias(true)
    draw(ctx, "COLOR", font: "Kanit-Bold", size: 112, x: 0, baseline: 92, target: 372, stretchGlyphs: true, fill: color(0x2BA7DF))
    draw(ctx, "PACK", font: "Kanit-Bold", size: 112, x: 388, baseline: 92, target: 292, stretchGlyphs: true, fill: pack)
    for (cx, c) in [(272, 0x3B8FE0), (310, 0x6CC04A), (348, 0xF7941C), (386, 0xEF2B2C)] as [(CGFloat, UInt32)] {
        ctx.setFillColor(color(c))
        ctx.fillEllipse(in: CGRect(x: (cx - 11) * S, y: (H - 118 - 11) * S, width: 22 * S, height: 22 * S))
    }
    draw(ctx, "CREATIONS", font: "Kanit-Light", size: 32, x: 2, baseline: 164, target: 676, stretchGlyphs: false,
         fill: pack.copy(alpha: pack.alpha * 0.75)!)
    let rep = NSBitmapImageRep(cgImage: ctx.makeImage()!)
    try! rep.representation(using: .png, properties: [:])!.write(to: URL(fileURLWithPath: outDir + "/" + name))
    print("wrote \(name)")
}

render(pack: color(0xFFFFFF), name: "colorpack-logo-on-dark.png")   // PACK สีขาว บนแถบสีเข้ม
render(pack: color(0x111111), name: "colorpack-logo-on-light.png")  // PACK สี ink บนพื้นขาว
