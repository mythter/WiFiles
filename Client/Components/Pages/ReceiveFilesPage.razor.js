export class ReceiveFilesPage {

    static speed = 12;
    static delay = 3000;

    static Init(container, folder) {
        let containerWidth = this.GetContainerWidth(container);
        document.documentElement.style.setProperty("--container-width", containerWidth + "px");

        this.SetAnimation(folder, container, this.speed, this.delay);

        new ResizeObserver(() => {
            let containerWidth = this.GetContainerWidth(container);
            document.documentElement.style.setProperty("--container-width", containerWidth + "px");
            this.CheckAnimation(folder, containerWidth, this.speed);
        }).observe(container);
    }

    static CheckAnimation(elem, containerWidth, speed) {
        elem.classList.remove("running-text");
        if (elem.clientWidth > containerWidth) {
            elem.classList.add("running-text");
            this.SetAnimationDuration(elem, containerWidth, speed);
        }
        //else {
        //    elem.classList.remove("running-text");
        //}
    }

    static SetAnimation(elem, container, speed, delay) {
        let containerWidth = this.GetContainerWidth(container);
        elem.classList.add("running-text");

        this.SetAnimationDuration(elem, containerWidth, speed)

        elem.addEventListener("animationiteration", () => {
            elem.style["animation-play-state"] = "paused";
            setTimeout(function () {
                elem.style["animation-play-state"] = "running";
            }, delay);
        });
    }

    static SetAnimationDuration(elem, containerWidth, speed) {
        let duration = (elem.clientWidth - containerWidth) / speed;
        elem.style["animation-duration"] = duration + "s";
    }

    static GetContainerWidth(container) {
        let infoPaddingRight = parseFloat(window.getComputedStyle(container, null).getPropertyValue('padding-right'));
        let infoPaddingLeft = parseFloat(window.getComputedStyle(container, null).getPropertyValue('padding-left'));
        return container.clientWidth - infoPaddingRight - infoPaddingLeft;
    }

    static FolderChanged(container, folder) {
        let containerWidth = this.GetContainerWidth(container);
        this.CheckAnimation(folder, containerWidth, this.speed);
    }
}