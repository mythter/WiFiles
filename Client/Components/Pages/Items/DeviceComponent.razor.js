export class Device {
    static Init(device, sendBtn) {
        if (device) {
            device.addEventListener("click", () => {
                sendBtn.classList.remove("hidden");
            });
            device.addEventListener("focusout", () => {
                sendBtn.classList.add("hidden");
            });
        }
    }
}