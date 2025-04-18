export class MessageBox {
    static Init(dotNet, container) {
        if (container) {
            container.addEventListener('animationend', (e) => {
                if (e.animationName.startsWith("disappearing")) {
                    dotNet.invokeMethodAsync("Clear");
                }
            });
        }
    }
}