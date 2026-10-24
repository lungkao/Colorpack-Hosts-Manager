// วาดไอคอนแอป Hosts Manager (1024x1024) — ลูกโลก + ลูกศรชี้ไป server ใหม่
// ใช้: swiftc icon.swift -o makeicon && ./makeicon icon_1024.png
import AppKit

let out = CommandLine.arguments.count > 1 ? CommandLine.arguments[1] : "icon_1024.png"
let size = 1024

func rgb(_ h: UInt32, _ a: CGFloat = 1) -> CGColor {
    CGColor(red: CGFloat((h >> 16) & 255) / 255,
            green: CGFloat((h >> 8) & 255) / 255,
            blue: CGFloat(h & 255) / 255,
            alpha: a)
}

let space = CGColorSpaceCreateDeviceRGB()
guard let ctx = CGContext(data: nil, width: size, height: size, bitsPerComponent: 8,
                          bytesPerRow: 0, space: space,
                          bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue) else { exit(1) }

// พื้นหลังทรง squircle ตามสัดส่วนไอคอน macOS (ขอบ 100px)
let base = CGRect(x: 100, y: 100, width: 824, height: 824)
let basePath = CGPath(roundedRect: base, cornerWidth: 186, cornerHeight: 186, transform: nil)
let bgGradient = CGGradient(colorsSpace: space,
                            colors: [rgb(0x4F46E5), rgb(0x0EA5E9), rgb(0x14B8A6)] as CFArray,
                            locations: [0, 0.6, 1])!
func drawBackground() {
    ctx.drawLinearGradient(bgGradient, start: CGPoint(x: 220, y: 924), end: CGPoint(x: 804, y: 100), options: [])
}

ctx.saveGState()
ctx.setShadow(offset: CGSize(width: 0, height: -14), blur: 30, color: rgb(0x000000, 0.35))
ctx.addPath(basePath)
ctx.setFillColor(rgb(0x1E3A8A))
ctx.fillPath()
ctx.restoreGState()

ctx.saveGState()
ctx.addPath(basePath)
ctx.clip()
drawBackground()
// ไฮไลต์ด้านบนเบาๆ
let hl = CGGradient(colorsSpace: space, colors: [rgb(0xFFFFFF, 0.22), rgb(0xFFFFFF, 0)] as CFArray, locations: [0, 1])!
ctx.drawLinearGradient(hl, start: CGPoint(x: 512, y: 924), end: CGPoint(x: 512, y: 560), options: [])

// ลูกโลก
let c = CGPoint(x: 468, y: 572)
let r: CGFloat = 248
let globe = CGRect(x: c.x - r, y: c.y - r, width: r * 2, height: r * 2)
ctx.setStrokeColor(rgb(0xFFFFFF))
ctx.setLineCap(.round)

ctx.saveGState()
ctx.addEllipse(in: globe.insetBy(dx: 10, dy: 10))
ctx.clip()
ctx.setFillColor(rgb(0xFFFFFF, 0.10))
ctx.fill(globe)
ctx.setLineWidth(24)
ctx.strokeEllipse(in: CGRect(x: c.x - r * 0.46, y: c.y - r, width: r * 0.92, height: r * 2))
ctx.move(to: CGPoint(x: c.x, y: c.y - r)); ctx.addLine(to: CGPoint(x: c.x, y: c.y + r))
for dy in [-0.5, 0.0, 0.5] {
    let y = c.y + r * CGFloat(dy)
    ctx.move(to: CGPoint(x: c.x - r, y: y)); ctx.addLine(to: CGPoint(x: c.x + r, y: y))
}
ctx.strokePath()
ctx.restoreGState()

ctx.setLineWidth(34)
ctx.strokeEllipse(in: globe)

// เจาะวงรอบป้ายด้วยสีพื้นหลัง ให้ป้ายลอยแยกจากลูกโลก
let bc = CGPoint(x: 716, y: 314)
let br: CGFloat = 150
ctx.saveGState()
ctx.addEllipse(in: CGRect(x: bc.x - br - 24, y: bc.y - br - 24, width: (br + 24) * 2, height: (br + 24) * 2))
ctx.clip()
drawBackground()
ctx.restoreGState()
ctx.restoreGState() // จบการ clip squircle

// ป้ายวงกลมสีขาว + ลูกศร
ctx.saveGState()
ctx.setShadow(offset: CGSize(width: 0, height: -8), blur: 18, color: rgb(0x0F172A, 0.30))
ctx.setFillColor(rgb(0xFFFFFF))
ctx.fillEllipse(in: CGRect(x: bc.x - br, y: bc.y - br, width: br * 2, height: br * 2))
ctx.restoreGState()

let arrow = CGMutablePath()
arrow.move(to: CGPoint(x: bc.x - 82, y: bc.y + 24))
arrow.addLine(to: CGPoint(x: bc.x + 6, y: bc.y + 24))
arrow.addLine(to: CGPoint(x: bc.x + 6, y: bc.y + 76))
arrow.addLine(to: CGPoint(x: bc.x + 90, y: bc.y))
arrow.addLine(to: CGPoint(x: bc.x + 6, y: bc.y - 76))
arrow.addLine(to: CGPoint(x: bc.x + 6, y: bc.y - 24))
arrow.addLine(to: CGPoint(x: bc.x - 82, y: bc.y - 24))
arrow.closeSubpath()
ctx.saveGState()
ctx.addPath(arrow)
ctx.setLineJoin(.round)
ctx.setLineWidth(18)
ctx.replacePathWithStrokedPath()
ctx.addPath(arrow)
ctx.clip()
let ag = CGGradient(colorsSpace: space, colors: [rgb(0x4F46E5), rgb(0x0EA5E9)] as CFArray, locations: [0, 1])!
ctx.drawLinearGradient(ag, start: CGPoint(x: bc.x - 90, y: bc.y + 80), end: CGPoint(x: bc.x + 100, y: bc.y - 80), options: [])
ctx.restoreGState()

guard let img = ctx.makeImage() else { exit(1) }
let rep = NSBitmapImageRep(cgImage: img)
guard let data = rep.representation(using: .png, properties: [:]) else { exit(1) }
try! data.write(to: URL(fileURLWithPath: out))
print("wrote \(out)")
