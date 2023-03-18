
(function () {
    // Inspired by: https://scottdorman.blog/2018/08/18/saving-bootstrap-component-state/
    const collapseStateKey = "collapse-state";

    function getFragmentKey() {
        if (window.location.hash.length > 0) {
            return window.location.hash.substr(1);
        } else {
            return "";
        }
    }

    function restoreCollapseState() {
        var state = localStorage.getItem(collapseStateKey);

        if (state) {
            state = JSON.parse(state);
            for (const id in state) {
                var el = document.getElementById(id);
                if (!el) {
                    continue;
                }

                var labelledBy = el.getAttribute('aria-labelledBy');
                var labelledByEl = document.getElementById(labelledBy);

                if (state[id]) {
                    el.classList.remove('show');
                    labelledByEl.classList.add('collapsed');
                } else {
                    el.classList.add('show');
                    labelledByEl.classList.remove('collapsed');
                }
            }
        }
    }

    function setCollapseState(id, hide) {
        var state = localStorage.getItem(collapseStateKey);
        if (!state) {
            state = {};
        } else {
            state = JSON.parse(state);
        }

        state[id] = hide;
        localStorage.setItem(collapseStateKey, JSON.stringify(state));
    }

    function getKeyFromContentId(id) {
        return id.substr(0, id.length - "-content".length);
    }

    $(function () {
        function setCheckboxes() {
            var card = $(this).parents('.card')
            var onlyLatestLeaves = card.find('input[name="onlyLatestLeaves"]');
            var useCustomCursor = card.find('input[name="useCustomCursor"]');
            var useBucketRanges = card.find('input[name="useBucketRanges"]');

            var customCursorChecked = false;
            var bucketRangesChecked = false;

            if (this == useCustomCursor[0]) {
                customCursorChecked = useCustomCursor.prop('checked');
            } else if (this == useBucketRanges[0]) {
                customCursorChecked = false;
                bucketRangesChecked = useBucketRanges.prop('checked');
            }
            
            useCustomCursor.prop('checked', customCursorChecked);
            card.find('.custom-cursor').toggle(customCursorChecked);
            card.find('button[name="overrideCursor"]').toggle(customCursorChecked);
            
            useBucketRanges.prop('checked', bucketRangesChecked);
            card.find('.bucket-ranges').toggle(bucketRangesChecked);
            
            onlyLatestLeaves.prop('disabled', bucketRangesChecked);
        }

        $('input[name="useCustomCursor"]').on('change', setCheckboxes);
        $('input[name="useBucketRanges"]').on('change', setCheckboxes);

        $('button').on('click', function () {
            if ($(this).hasClass('btn-danger')) {
                var message = $(this).data('message');
                if (!message) {
                    message = "Are you sure you want to do this?";
                }

                return confirm(message);
            }
        })

        $('.collapse-remember').on('hide.bs.collapse show.bs.collapse', function (e) {
            $(this).data('kc.hidden', e.type == 'hide');
            setCollapseState(this.id, e.type == 'hide');
        });

        $('.collapse-remember').on('hide.bs.collapse', function (e) {
            if (getKeyFromContentId(this.id) == getFragmentKey()) {
                history.replaceState("", document.title, window.location.pathname + window.location.search);
            }
        });

        var fragmentKey = getFragmentKey();
        if (fragmentKey) {
            $('#' + fragmentKey + '-content').collapse('show');
        }

        $('.card [data-toggle="collapse"]').dblclick(function () {
            var hidden = $(this).parents(".card").find('.collapse-remember').data('kc.hidden');
            $('.collapse-remember').collapse(hidden ? 'hide' : 'show');
        });
    });

    restoreCollapseState();
})();
