export class Device {
    static Init(device, sendBtn) {
        device.addEventListener("click", () => {
            sendBtn.classList.remove("hidden");
        });
        device.addEventListener("focusout", () => {
            sendBtn.classList.add("hidden");
        });
    }
}