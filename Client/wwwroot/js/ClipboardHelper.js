export class ClipboardHelper {

    static CopyToClipboard(text) {
        navigator.clipboard.writeText(text);
    }

    static async ReadFromClipboard() {
        return await navigator.clipboard.readText();
    }
}